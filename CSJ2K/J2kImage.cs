// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

using System.Linq;

namespace CSJ2K
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    using CSJ2K.Color;
    using CSJ2K.Icc;
    using CSJ2K.j2k.codestream;
    using CSJ2K.j2k.codestream.reader;
    using CSJ2K.j2k.codestream.writer;
    using CSJ2K.j2k.decoder;
    using CSJ2K.j2k.encoder;
    using CSJ2K.j2k.entropy.decoder;
    using CSJ2K.j2k.entropy.encoder;
    using CSJ2K.j2k.fileformat.reader;
    using CSJ2K.j2k.fileformat.writer;
    using CSJ2K.j2k.image;
    using CSJ2K.j2k.image.forwcomptransf;
    using CSJ2K.j2k.image.input;
    using CSJ2K.j2k.image.invcomptransf;
    using CSJ2K.j2k.io;
    using CSJ2K.j2k.quantization.dequantizer;
    using CSJ2K.j2k.quantization.quantizer;
    using CSJ2K.j2k.roi;
    using CSJ2K.j2k.roi.encoder;
    using CSJ2K.j2k.util;
    using CSJ2K.j2k.wavelet.analysis;
    using CSJ2K.j2k.wavelet.synthesis;
    using CSJ2K.Util;

    public class J2kImage
    {

        #region Static Decoder Methods

        public static PortableImage FromFile(string filename, ParameterList parameters = null)
        {
            using (var stream = FileStreamFactory.New(filename, "r"))
            {
                return FromStream(stream, parameters);
            }
        }

        public static PortableImage FromBytes(byte[] j2kdata, ParameterList parameters = null)
        {
            using (var stream = new MemoryStream(j2kdata))
            {
                return FromStream(stream, parameters);
            }
        }

        public static PortableImage FromStream(Stream stream, ParameterList parameters = null)
        {
            RandomAccessIO in_stream = new ISRandomAccessIO(stream);

            // Initialize default parameters
            ParameterList defpl = GetDefaultDecoderParameterList(decoder_pinfo);

            // Create parameter list using defaults
            ParameterList pl = parameters ?? new ParameterList(defpl);

            // **** File Format ****
            // If the codestream is wrapped in the jp2 fileformat, Read the
            // file format wrapper
            FileFormatReader ff = new FileFormatReader(in_stream);
            ff.readFileFormat();
            if (ff.JP2FFUsed)
            {
                in_stream.seek(ff.FirstCodeStreamPos);
            }

            // +----------------------------+
            // | Instantiate decoding chain |
            // +----------------------------+

            // **** Header decoder ****
            // Instantiate header decoder and read main header 
            HeaderInfo hi = new HeaderInfo();
            HeaderDecoder hd;
            try
            {
                hd = new HeaderDecoder(in_stream, pl, hi);
            }
            catch (EndOfStreamException e)
            {
                throw new InvalidOperationException("Codestream too short or bad header, unable to decode.", e);
            }

            int nCompCod = hd.NumComps;
            int nTiles = hi.sizValue.NumTiles;
            DecoderSpecs decSpec = hd.DecoderSpecs;

            // Get demixed bitdepths
            int[] depth = new int[nCompCod];
            for (int i = 0; i < nCompCod; i++)
            {
                depth[i] = hd.getOriginalBitDepth(i);
            }

            // **** Bit stream reader ****
            BitstreamReaderAgent breader;
            try
            {
                breader = BitstreamReaderAgent.createInstance(in_stream, hd, pl, decSpec, false, hi);
            }
            catch (IOException e)
            {
                throw new InvalidOperationException("Error while reading bit stream header or parsing packets.", e);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate bit stream reader.", e);
            }

            // **** Entropy decoder ****
            EntropyDecoder entdec;
            try
            {
                entdec = hd.createEntropyDecoder(breader, pl);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate entropy decoder.", e);
            }

            // **** ROI de-scaler ****
            ROIDeScaler roids;
            try
            {
                roids = hd.createROIDeScaler(entdec, pl, decSpec);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate roi de-scaler.", e);
            }

            // **** Dequantizer ****
            Dequantizer deq;
            try
            {
                deq = hd.createDequantizer(roids, depth, decSpec);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate dequantizer.", e);
            }

            // **** Inverse wavelet transform ***
            InverseWT invWT;
            try
            {
                // full page inverse wavelet transform
                invWT = InverseWT.createInstance(deq, decSpec);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("Cannot instantiate inverse wavelet transform.", e);
            }

            int res = breader.ImgRes;
            invWT.ImgResLevel = res;

            // **** Data converter **** (after inverse transform module)
            ImgDataConverter converter = new ImgDataConverter(invWT, 0);

            // **** Inverse component transformation **** 
            InvCompTransf ictransf = new InvCompTransf(converter, decSpec, depth, pl);

            // **** Color space mapping ****
            BlkImgDataSrc color;
            if (ff.JP2FFUsed && pl.getParameter("nocolorspace").Equals("off"))
            {
                try
                {
                    ColorSpace csMap = new ColorSpace(in_stream, hd, pl);
                    BlkImgDataSrc channels = hd.createChannelDefinitionMapper(ictransf, csMap);
                    BlkImgDataSrc resampled = hd.createResampler(channels, csMap);
                    BlkImgDataSrc palettized = hd.createPalettizedColorSpaceMapper(resampled, csMap);
                    color = hd.createColorSpaceMapper(palettized, csMap);
                }
                catch (ArgumentException e)
                {
                    throw new InvalidOperationException("Could not instantiate ICC profiler.", e);
                }
                catch (ColorSpaceException e)
                {
                    throw new InvalidOperationException("Error processing ColorSpace information.", e);
                }
            }
            else
            {
                // Skip colorspace mapping
                color = ictransf;
            }

            // This is the last image in the decoding chain and should be
            // assigned by the last transformation:
            BlkImgDataSrc decodedImage = color;
            if (color == null)
            {
                decodedImage = ictransf;
            }
            var numComps = decodedImage.NumComps;
            var imgWidth = decodedImage.ImgWidth;

            // **** Copy to Bitmap ****

            var bitsUsed = new int[numComps];
            for (var j = 0; j < numComps; ++j) bitsUsed[j] = decodedImage.getNomRangeBits(numComps - 1 - j);

            var dst = new PortableImage(imgWidth, decodedImage.ImgHeight, numComps, bitsUsed);

            Coord numTiles = decodedImage.getNumTiles(null);

            int tIdx = 0;

            for (int y = 0; y < numTiles.y; y++)
            {
                // Loop on horizontal tiles
                for (int x = 0; x < numTiles.x; x++, tIdx++)
                {
                    decodedImage.setTile(x, y);

                    int height = decodedImage.getTileCompHeight(tIdx, 0);
                    int width = decodedImage.getTileCompWidth(tIdx, 0);

                    int tOffx = decodedImage.getCompULX(0)
                                - (int)Math.Ceiling(decodedImage.ImgULX / (double)decodedImage.getCompSubsX(0));

                    int tOffy = decodedImage.getCompULY(0)
                                - (int)Math.Ceiling(decodedImage.ImgULY / (double)decodedImage.getCompSubsY(0));

                    DataBlkInt[] db = new DataBlkInt[numComps];
                    int[] ls = new int[numComps];
                    int[] mv = new int[numComps];
                    int[] fb = new int[numComps];
                    for (int i = 0; i < numComps; i++)
                    {
                        db[i] = new DataBlkInt();
                        ls[i] = 1 << (decodedImage.getNomRangeBits(0) - 1);
                        mv[i] = (1 << decodedImage.getNomRangeBits(0)) - 1;
                        fb[i] = decodedImage.getFixedPoint(0);
                    }
                    for (int l = 0; l < height; l++)
                    {
                        for (int i = numComps - 1; i >= 0; i--)
                        {
                            db[i].ulx = 0;
                            db[i].uly = l;
                            db[i].w = width;
                            db[i].h = 1;
                            decodedImage.getInternCompData(db[i], i);
                        }
                        int[] k = new int[numComps];
                        for (int i = numComps - 1; i >= 0; i--) k[i] = db[i].offset + width - 1;

                        var rowvalues = new int[width * numComps];

                        for (int i = width - 1; i >= 0; i--)
                        {
                            int[] tmp = new int[numComps];
                            for (int j = numComps - 1; j >= 0; j--)
                            {
                                tmp[j] = (db[j].data_array[k[j]--] >> fb[j]) + ls[j];
                                tmp[j] = (tmp[j] < 0) ? 0 : ((tmp[j] > mv[j]) ? mv[j] : tmp[j]);

                            }
                            var offset = i * numComps;
                            switch (numComps)
                            {
                                case 1:
                                    rowvalues[offset + 0] = tmp[0];
                                    break;
                                case 3:
                                    rowvalues[offset + 0] = tmp[2];
                                    rowvalues[offset + 1] = tmp[1];
                                    rowvalues[offset + 2] = tmp[0];
                                    break;
                                case 4:
                                    rowvalues[offset + 0] = tmp[3];
                                    rowvalues[offset + 1] = tmp[2];
                                    rowvalues[offset + 2] = tmp[1];
                                    rowvalues[offset + 3] = tmp[0];
                                    break;
                                default:
                                    throw new InvalidOperationException($"Invalid number of components: {numComps}");
                            }
                        }

                        dst.FillRow(tOffx, tOffy + l, imgWidth, rowvalues);
                    }
                }
            }

            return dst;
        }

        #endregion

        #region Static Encoder Methods

        public static BlkImgDataSrc CreateEncodableSource(Stream stream)
        {
            return CreateEncodableSource(new[] { stream });
        }

        public static BlkImgDataSrc CreateEncodableSource(IEnumerable<Stream> streams)
        {
            if (streams == null)
            {
                throw new ArgumentNullException("streams");
            }

            var counter = 0;
            var ncomp = 0;
            var ppminput = false;
            var imageReaders = new List<ImgReader>();

            foreach (var stream in streams)
            {
                ++counter;
                var imgType = GetImageType(stream);

                switch (imgType)
                {
                    case "P5":
                        imageReaders.Add(new ImgReaderPGM(stream));
                        ncomp += 1;
                        break;
                    case "P6":
                        imageReaders.Add(new ImgReaderPPM(stream));
                        ncomp += 3;
                        ppminput = true;
                        break;
                    case "PG":
                        imageReaders.Add(new ImgReaderPGX(stream));
                        ncomp += 1;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("streams", "Invalid image type");
                }
            }

            if (ppminput && counter > 1)
            {
                error("With PPM input format only 1 input file can be specified", 2);
                return null;
            }

            BlkImgDataSrc imgsrc;

            // **** ImgDataJoiner (if needed) ****
            if (ppminput || ncomp == 1)
            {
                // Just one input
                imgsrc = imageReaders[0];
            }
            else
            {
                // More than one reader => join all readers into 1
                var imgcmpidxs = new int[ncomp];
                imgsrc = new ImgDataJoiner(imageReaders, imgcmpidxs);
            }

            return imgsrc;
        }

        public static byte[] ToBytes(object imageObject, ParameterList parameters = null)
        {
            var imgsrc = ImageFactory.ToPortableImageSource(imageObject);
            return ToBytes(imgsrc, parameters);
        }

        public static byte[] ToBytes(BlkImgDataSrc imgsrc, ParameterList parameters = null)
        {
            // Initialize default parameters
            ParameterList defpl = GetDefaultEncoderParameterList(encoder_pinfo);

            // Create parameter list using defaults
            ParameterList pl = parameters ?? new ParameterList(defpl);

            bool useFileFormat = false;
            bool pphTile = false;
            bool pphMain = false;
            bool tempSop = false;
            bool tempEph = false;

            // **** Get general parameters ****

            if (pl.getParameter("file_format").Equals("on"))
            {
                useFileFormat = true;
                if (pl.getParameter("rate") != null && pl.getFloatParameter("rate") != defpl.getFloatParameter("rate"))
                {
                    warning("Specified bit-rate applies only on the codestream but not on the whole file.");
                }
            }

            if (pl.getParameter("tiles") == null)
            {
                error("No tiles option specified", 2);
                return null;
            }

            if (pl.getParameter("pph_tile").Equals("on"))
            {
                pphTile = true;

                if (pl.getParameter("Psop").Equals("off"))
                {
                    pl["Psop"] = "on";
                    tempSop = true;
                }
                if (pl.getParameter("Peph").Equals("off"))
                {
                    pl["Peph"] = "on";
                    tempEph = true;
                }
            }

            if (pl.getParameter("pph_main").Equals("on"))
            {
                pphMain = true;

                if (pl.getParameter("Psop").Equals("off"))
                {
                    pl["Psop"] = "on";
                    tempSop = true;
                }
                if (pl.getParameter("Peph").Equals("off"))
                {
                    pl["Peph"] = "on";
                    tempEph = true;
                }
            }

            if (pphTile && pphMain) error("Can't have packed packet headers in both main and" + " tile headers", 2);

            if (pl.getBooleanParameter("lossless") && pl.getParameter("rate") != null
                && pl.getFloatParameter("rate") != defpl.getFloatParameter("rate")) throw new ArgumentException("Cannot use '-rate' and " + "'-lossless' option at " + " the same time.");

            if (pl.getParameter("rate") == null)
            {
                error("Target bitrate not specified", 2);
                return null;
            }
            float rate;
            try
            {
                rate = pl.getFloatParameter("rate");
                if (rate == -1)
                {
                    rate = float.MaxValue;
                }
            }
            catch (FormatException e)
            {
                error("Invalid value in 'rate' option: " + pl.getParameter("rate"), 2);
                return null;
            }
            int pktspertp;
            try
            {
                pktspertp = pl.getIntParameter("tile_parts");
                if (pktspertp != 0)
                {
                    if (pl.getParameter("Psop").Equals("off"))
                    {
                        pl["Psop"] = "on";
                        tempSop = true;
                    }
                    if (pl.getParameter("Peph").Equals("off"))
                    {
                        pl["Peph"] = "on";
                        tempEph = true;
                    }
                }
            }
            catch (FormatException e)
            {
                error("Invalid value in 'tile_parts' option: " + pl.getParameter("tile_parts"), 2);
                return null;
            }

            // **** ImgReader ****
            var ncomp = imgsrc.NumComps;
            var ppminput = imgsrc.NumComps > 1;

            // **** Tiler ****
            // get nominal tile dimensions
            SupportClass.StreamTokenizerSupport stok =
                new SupportClass.StreamTokenizerSupport(new StringReader(pl.getParameter("tiles")));
            stok.EOLIsSignificant(false);

            stok.NextToken();
            if (stok.ttype != SupportClass.StreamTokenizerSupport.TT_NUMBER)
            {
                error("An error occurred while parsing the tiles option: " + pl.getParameter("tiles"), 2);
                return null;
            }
            var tw = (int)stok.nval;
            stok.NextToken();
            if (stok.ttype != SupportClass.StreamTokenizerSupport.TT_NUMBER)
            {
                error("An error occurred while parsing the tiles option: " + pl.getParameter("tiles"), 2);
                return null;
            }
            var th = (int)stok.nval;

            // Get image reference point
            var refs = pl.getParameter("ref").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int refx;
            int refy;
            try
            {
                refx = Int32.Parse(refs[0]);
                refy = Int32.Parse(refs[1]);
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ArgumentException("Error while parsing 'ref' " + "option");
            }
            catch (FormatException e)
            {
                throw new ArgumentException("Invalid number type in " + "'ref' option");
            }
            if (refx < 0 || refy < 0)
            {
                throw new ArgumentException("Invalid value in 'ref' " + "option ");
            }

            // Get tiling reference point
            var trefs = pl.getParameter("tref").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int trefx;
            int trefy;
            try
            {
                trefx = Int32.Parse(trefs[0]);
                trefy = Int32.Parse(trefs[1]);
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ArgumentException("Error while parsing 'tref' " + "option");
            }
            catch (FormatException e)
            {
                throw new ArgumentException("Invalid number type in " + "'tref' option");
            }
            if (trefx < 0 || trefy < 0 || trefx > refx || trefy > refy)
            {
                throw new ArgumentException("Invalid value in 'tref' " + "option ");
            }

            // Instantiate tiler
            Tiler imgtiler;
            try
            {
                imgtiler = new Tiler(imgsrc, refx, refy, trefx, trefy, tw, th);
            }
            catch (ArgumentException e)
            {
                error("Could not tile image" + ((e.Message != null) ? (":\n" + e.Message) : ""), 2);
                return null;
            }
            int ntiles = imgtiler.getNumTiles();

            // **** Encoder specifications ****
            var encSpec = new EncoderSpecs(ntiles, ncomp, imgsrc, pl);

            // **** Component transformation ****
            if (ppminput && pl.getParameter("Mct") != null && pl.getParameter("Mct").Equals("off"))
            {
                FacilityManager.getMsgLogger()
                    .printmsg(
                        MsgLogger_Fields.WARNING,
                        "Input image is RGB and no color transform has "
                        + "been specified. Compression performance and "
                        + "image quality might be greatly degraded. Use "
                        + "the 'Mct' option to specify a color transform");
            }
            ForwCompTransf fctransf;
            try
            {
                fctransf = new ForwCompTransf(imgtiler, encSpec);
            }
            catch (ArgumentException e)
            {
                error(
                    "Could not instantiate forward component " + "transformation"
                    + ((e.Message != null) ? (":\n" + e.Message) : ""),
                    2);
                return null;
            }

            // **** ImgDataConverter ****
            var converter = new ImgDataConverter(fctransf);


            // **** ForwardWT ****
            ForwardWT dwt;
            try
            {
                dwt = ForwardWT.createInstance(converter, pl, encSpec);
            }
            catch (ArgumentException e)
            {
                error("Could not instantiate wavelet transform" + ((e.Message != null) ? (":\n" + e.Message) : ""), 2);
                return null;
            }

            // **** Quantizer ****
            Quantizer quant;
            try
            {
                quant = Quantizer.createInstance(dwt, encSpec);
            }
            catch (ArgumentException e)
            {
                error("Could not instantiate quantizer" + ((e.Message != null) ? (":\n" + e.Message) : ""), 2);
                return null;
            }

            // **** ROIScaler ****
            ROIScaler rois;
            try
            {
                rois = ROIScaler.createInstance(quant, pl, encSpec);
            }
            catch (ArgumentException e)
            {
                error("Could not instantiate ROI scaler" + ((e.Message != null) ? (":\n" + e.Message) : ""), 2);
                return null;
            }

            // **** EntropyCoder ****
            EntropyCoder ecoder;
            try
            {
                ecoder = EntropyCoder.createInstance(
                    rois,
                    pl,
                    encSpec.cblks,
                    encSpec.pss,
                    encSpec.bms,
                    encSpec.mqrs,
                    encSpec.rts,
                    encSpec.css,
                    encSpec.sss,
                    encSpec.lcs,
                    encSpec.tts);
            }
            catch (ArgumentException e)
            {
                error("Could not instantiate entropy coder" + ((e.Message != null) ? (":\n" + e.Message) : ""), 2);
                return null;
            }

            // **** CodestreamWriter ****
            using (var outStream = new MemoryStream())
            {
                CodestreamWriter bwriter;
                try
                {
                    // Rely on rate allocator to limit amount of data
                    bwriter = new FileCodestreamWriter(outStream, Int32.MaxValue);
                }
                catch (IOException e)
                {
                    error("Could not open output file" + ((e.Message != null) ? (":\n" + e.Message) : ""), 2);
                    return null;
                }

                // **** Rate allocator ****
                PostCompRateAllocator ralloc;
                try
                {
                    ralloc = PostCompRateAllocator.createInstance(ecoder, pl, rate, bwriter, encSpec);
                }
                catch (ArgumentException e)
                {
                    error("Could not instantiate rate allocator" + ((e.Message != null) ? (":\n" + e.Message) : ""), 2);
                    return null;
                }

                // **** HeaderEncoder ****
                var imsigned = Enumerable.Repeat(false, ncomp).ToArray();   // TODO Consider supporting signed components.
                var headenc = new HeaderEncoder(imgsrc, imsigned, dwt, imgtiler, encSpec, rois, ralloc, pl);
                ralloc.HeaderEncoder = headenc;

                // **** Write header to be able to estimate header overhead ****
                headenc.encodeMainHeader();

                // **** Initialize rate allocator, with proper header
                // overhead. This will also encode all the data ****
                ralloc.initialize();

                // **** Write header (final) ****
                headenc.reset();
                headenc.encodeMainHeader();

                // Insert header into the codestream
                bwriter.commitBitstreamHeader(headenc);

                // **** Now do the rate-allocation and write result ****
                ralloc.runAndWrite();

                // **** Done ****
                bwriter.close();

                // **** Calculate file length ****
                int fileLength = bwriter.Length;

                // **** Tile-parts and packed packet headers ****
                if (pktspertp > 0 || pphTile || pphMain)
                {
                    try
                    {
                        CodestreamManipulator cm = new CodestreamManipulator(
                            outStream,
                            ntiles,
                            pktspertp,
                            pphMain,
                            pphTile,
                            tempSop,
                            tempEph);
                        fileLength += cm.doCodestreamManipulation();
                        //String res="";
                        if (pktspertp > 0)
                        {
                            FacilityManager.getMsgLogger()
                                .println(
                                    "Created tile-parts " + "containing at most " + pktspertp + " packets per tile.",
                                    4,
                                    6);
                        }
                        if (pphTile)
                        {
                            FacilityManager.getMsgLogger().println("Moved packet headers " + "to tile headers", 4, 6);
                        }
                        if (pphMain)
                        {
                            FacilityManager.getMsgLogger().println("Moved packet headers " + "to main header", 4, 6);
                        }
                    }
                    catch (IOException e)
                    {
                        error(
                            "Error while creating tileparts or packed packet" + " headers"
                            + ((e.Message != null) ? (":\n" + e.Message) : ""),
                            2);
                        return null;
                    }
                }

                // **** File Format ****
                if (useFileFormat)
                {
                    try
                    {
                        int nc = imgsrc.NumComps;
                        int[] bpc = new int[nc];
                        for (int comp = 0; comp < nc; comp++)
                        {
                            bpc[comp] = imgsrc.getNomRangeBits(comp);
                        }

                        outStream.Seek(0, SeekOrigin.Begin);
                        var ffw = new FileFormatWriter(
                            outStream,
                            imgsrc.ImgHeight,
                            imgsrc.ImgWidth,
                            nc,
                            bpc,
                            fileLength);
                        fileLength += ffw.writeFileFormat();
                    }
                    catch (IOException e)
                    {
                        throw new InvalidOperationException("Error while writing JP2 file format: " + e.Message);
                    }
                }

                // **** Close image readers ***
                imgsrc.close();

                return outStream.ToArray();
            }
        }

        #endregion

        #region Default Parameter Loaders

        public static ParameterList GetDefaultDecoderParameterList(string[][] pinfo)
        {
            ParameterList pl = new ParameterList();
            string[][] str;

            str = BitstreamReaderAgent.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = EntropyDecoder.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = ROIDeScaler.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = Dequantizer.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = InvCompTransf.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = HeaderDecoder.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = ICCProfiler.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = pinfo ?? decoder_pinfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            return pl;
        }

        public static ParameterList GetDefaultEncoderParameterList(string[][] pinfo)
        {
            ParameterList pl = new ParameterList();
            string[][] str;

            str = pinfo ?? encoder_pinfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = ForwCompTransf.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = AnWTFilter.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = ForwardWT.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = Quantizer.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = ROIScaler.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = EntropyCoder.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = HeaderEncoder.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = PostCompRateAllocator.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            str = PktEncoder.ParameterInfo;
            if (str != null) for (int i = str.Length - 1; i >= 0; i--) pl[str[i][0]] = str[i][3];

            return pl;
        }

        #endregion

        #region Decoder Parameters

        private static String[][] decoder_pinfo =
            {
                new string[]
                    {
                        "u", "[on|off]",
                        "Prints usage information. "
                        + "If specified all other arguments (except 'v') are ignored",
                        "off"
                    },
                new string[]
                    {
                        "v", "[on|off]", "Prints version and copyright information",
                        "off"
                    },
                new string[]
                    {
                        "verbose", "[on|off]",
                        "Prints information about the decoded codestream", "on"
                    },
                new string[]
                    {
                        "pfile", "<filename>",
                        "Loads the arguments from the specified file. Arguments that are "
                        + "specified on the command line override the ones from the file.\n"
                        + "The arguments file is a simple text file with one argument per "
                        + "line of the following form:\n"
                        + "  <argument name>=<argument value>\n"
                        + "If the argument is of boolean type (i.e. its presence turns a "
                        + "feature on), then the 'on' value turns it on, while the 'off' "
                        + "value turns it off. The argument name does not include the '-' "
                        + "or '+' character. Long lines can be broken into several lines "
                        + "by terminating them with '\\'. Lines starting with '#' are "
                        + "considered as comments. This option is not recursive: any 'pfile' "
                        + "argument appearing in the file is ignored.",
                        null
                    },
                new string[]
                    {
                        "res", "<resolution level index>",
                        "The resolution level at which to reconstruct the image "
                        + " (0 means the lowest available resolution whereas the maximum "
                        + "resolution level corresponds to the original image resolution). "
                        + "If the given index"
                        + " is greater than the number of available resolution levels of the "
                        + "compressed image, the image is reconstructed at its highest "
                        + "resolution (among all tile-components). Note that this option"
                        + " affects only the inverse wavelet transform and not the number "
                        + " of bytes read by the codestream parser: this number of bytes "
                        + "depends only on options '-nbytes' or '-rate'.",
                        null
                    },
                new string[]
                    {
                        "i", "<filename or url>",
                        "The file containing the JPEG 2000 compressed data. This can be "
                        + "either a JPEG 2000 codestream or a JP2 file containing a "
                        + "JPEG 2000 "
                        + "codestream. In the latter case the first codestream in the file "
                        + "will be decoded. If an URL is specified (e.g., http://...) "
                        + "the data will be downloaded and cached in memory before decoding. "
                        + "This is intended for easy use in applets, but it is not a very "
                        + "efficient way of decoding network served data.",
                        null
                    },
                new string[]
                    {
                        "o", "<filename>",
                        "This is the name of the file to which the decompressed image "
                        + "is written. If no output filename is given, the image is "
                        + "displayed on the screen. "
                        + "Output file format is PGX by default. If the extension"
                        + " is '.pgm' then a PGM file is written as output, however this is "
                        + "only permitted if the component bitdepth does not exceed 8. If "
                        + "the extension is '.ppm' then a PPM file is written, however this "
                        + "is only permitted if there are 3 components and none of them has "
                        + "a bitdepth of more than 8. If there is more than 1 component, "
                        + "suffices '-1', '-2', '-3', ... are added to the file name, just "
                        + "before the extension, except for PPM files where all three "
                        + "components are written to the same file.",
                        null
                    },
                new string[]
                    {
                        "rate", "<decoding rate in bpp>",
                        "Specifies the decoding rate in bits per pixel (bpp) where the "
                        + "number of pixels is related to the image's original size (Note:"
                        + " this number is not affected by the '-res' option). If it is equal"
                        + "to -1, the whole codestream is decoded. "
                        + "The codestream is either parsed (default) or truncated depending "
                        + "the command line option '-parsing'. To specify the decoding "
                        + "rate in bytes, use '-nbytes' options instead.",
                        "-1"
                    },
                new string[]
                    {
                        "nbytes", "<decoding rate in bytes>",
                        "Specifies the decoding rate in bytes. "
                        + "The codestream is either parsed (default) or truncated depending "
                        + "the command line option '-parsing'. To specify the decoding "
                        + "rate in bits per pixel, use '-rate' options instead.",
                        "-1"
                    },
                new string[]
                    {
                        "parsing", null,
                        "Enable or not the parsing mode when decoding rate is specified "
                        + "('-nbytes' or '-rate' options). If it is false, the codestream "
                        + "is decoded as if it were truncated to the given rate. If it is "
                        + "true, the decoder creates, truncates and decodes a virtual layer"
                        + " progressive codestream with the same truncation points in each "
                        + "code-block.",
                        "on"
                    },
                new string[]
                    {
                        "ncb_quit", "<max number of code blocks>",
                        "Use the ncb and lbody quit conditions. If state information is "
                        + "found for more code blocks than is indicated with this option, "
                        + "the decoder "
                        + "will decode using only information found before that point. "
                        + "Using this otion implies that the 'rate' or 'nbyte' parameter "
                        + "is used to indicate the lbody parameter which is the number of "
                        + "packet body bytes the decoder will decode.",
                        "-1"
                    },
                new string[]
                    {
                        "l_quit", "<max number of layers>",
                        "Specifies the maximum number of layers to decode for any code-"
                        + "block",
                        "-1"
                    },
                new string[]
                    {
                        "m_quit", "<max number of bit planes>",
                        "Specifies the maximum number of bit planes to decode for any code"
                        + "-block",
                        "-1"
                    },
                new string[]
                    {
                        "poc_quit", null,
                        "Specifies the whether the decoder should only decode code-blocks "
                        + "included in the first progression order.",
                        "off"
                    },
                new string[]
                    {
                        "one_tp", null,
                        "Specifies whether the decoder should only decode the first "
                        + "tile part of each tile.",
                        "off"
                    },
                new string[]
                    {
                        "comp_transf", null,
                        "Specifies whether the component transform indicated in the "
                        + "codestream should be used.",
                        "on"
                    },
                new string[]
                    {
                        "debug", null,
                        "Print debugging messages when an error is encountered.",
                        "off"
                    },
                new string[]
                    {
                        "cdstr_info", null,
                        "Display information about the codestream. This information is: "
                        + "\n- Marker segments value in main and tile-part headers,"
                        + "\n- Tile-part length and position within the code-stream.",
                        "off"
                    },
                new string[]
                    {
                        "nocolorspace", null,
                        "Ignore any colorspace information in the image.", "off"
                    },
                new string[]
                    {
                        "colorspace_debug", null,
                        "Print debugging messages when an error is encountered in the"
                        + " colorspace module.",
                        "off"
                    }
            };

        #endregion

        #region Encoder Parameters

        private static String[][] encoder_pinfo =
            {
                new string[]
                    {
                        "debug", null,
                        "Print debugging messages when an error is encountered.",
                        "off"
                    },
                new string[]
                    {
                        "disable_jp2_extension", "[on|off]",
                        "JJ2000 automatically adds .jp2 extension when using 'file_format'"
                        + "option. This option disables it when on.",
                        "off"
                    },
                new string[]
                    {
                        "file_format", "[on|off]",
                        "Puts the JPEG 2000 codestream in a JP2 file format wrapper.",
                        "on"
                    },
                new string[]
                    {
                        "pph_tile", "[on|off]",
                        "Packs the packet headers in the tile headers.", "off"
                    },
                new string[]
                    {
                        "pph_main", "[on|off]",
                        "Packs the packet headers in the main header.", "off"
                    },
                new string[]
                    {
                        "pfile", "<filename of arguments file>",
                        "Loads the arguments from the specified file. Arguments that are "
                        + "specified on the command line override the ones from the file.\n"
                        + "The arguments file is a simple text file with one argument per "
                        + "line of the following form:\n"
                        + "  <argument name>=<argument value>\n"
                        + "If the argument is of boolean type (i.e. its presence turns a "
                        + "feature on), then the 'on' value turns it on, while the 'off' "
                        + "value turns it off. The argument name does not include the '-' "
                        + "or '+' character. Long lines can be broken into several lines "
                        + "by terminating them with '\'. Lines starting with '#' are "
                        + "considered as comments. This option is not recursive: any 'pfile' "
                        + "argument appearing in the file is ignored.",
                        null
                    },
                new string[]
                    {
                        "tile_parts", "<packets per tile-part>",
                        "This option specifies the maximum number of packets to have in "
                        + "one tile-part. 0 means include all packets in first tile-part "
                        + "of each tile",
                        "0"
                    },
                new string[]
                    {
                        "tiles", "<nominal tile width> <nominal tile height>",
                        "This option specifies the maximum tile dimensions to use. "
                        + "If both dimensions are 0 then no tiling is used.",
                        "0 0"
                    },
                new string[]
                    {
                        "ref", "<x> <y>",
                        "Sets the origin of the image in the canvas system. It sets the "
                        + "coordinate of the top-left corner of the image reference grid, "
                        + "with respect to the canvas origin",
                        "0 0"
                    },
                new string[]
                    {
                        "tref", "<x> <y>",
                        "Sets the origin of the tile partitioning on the reference grid, "
                        + "with respect to the canvas origin. The value of 'x' ('y') "
                        + "specified can not be larger than the 'x' one specified in the ref "
                        + "option.",
                        "0 0"
                    },
                new string[]
                    {
                        "rate", "<output bitrate in bpp>",
                        "This is the output bitrate of the codestream in bits per pixel."
                        + " When equal to -1, no image information (beside quantization "
                        + "effects) is discarded during compression.\n"
                        + "Note: In the case where '-file_format' option is used, the "
                        + "resulting file may have a larger bitrate.",
                        "-1"
                    },
                new string[]
                    {
                        "lossless", "[on|off]",
                        "Specifies a lossless compression for the encoder. This options"
                        + " is equivalent to use reversible quantization ('-Qtype "
                        + "reversible')"
                        + " and 5x3 wavelet filters pair ('-Ffilters w5x3'). Note that "
                        + "this option cannot be used with '-rate'. When this option is "
                        + "off, the quantization type and the filters pair is defined by "
                        + "'-Qtype' and '-Ffilters' respectively.",
                        "off"
                    },
                new string[]
                    {
                        "i", "<image file> [,<image file> [,<image file> ... ]]",
                        "Mandatory argument. This option specifies the name of the input "
                        + "image files. If several image files are provided, they have to be"
                        + " separated by commas in the command line. Supported formats are "
                        + "PGM (raw), PPM (raw) and PGX, "
                        + "which is a simple extension of the PGM file format for single "
                        + "component data supporting arbitrary bitdepths. If the extension "
                        + "is '.pgm', PGM-raw file format is assumed, if the extension is "
                        + "'.ppm', PPM-raw file format is assumed, otherwise PGX file "
                        + "format is assumed. PGM and PPM files are assumed to be 8 bits "
                        + "deep. A multi-component image can be specified by either "
                        + "specifying several PPM and/or PGX files, or by specifying one "
                        + "PPM file.",
                        null
                    },
                new string[]
                    {
                        "o", "<file name>",
                        "Mandatory argument. This option specifies the name of the output "
                        + "file to which the codestream will be written.",
                        null
                    },
                new string[]
                    {
                        "verbose", null,
                        "Prints information about the obtained bit stream.", "on"
                    },
                new string[]
                    {
                        "v", "[on|off]", "Prints version and copyright information.",
                        "off"
                    },
                new string[]
                    {
                        "u", "[on|off]",
                        "Prints usage information. "
                        + "If specified all other arguments (except 'v') are ignored",
                        "off"
                    },
            };

        #endregion

        /**
     * Prints the error message 'msg' to standard err, prepending "ERROR" to
     * it, and sets the exitCode to 'code'. An exit code different than 0
     * indicates that there where problems.
     *
     * @param msg The error message
     *
     * @param code The exit code to set
     * */

        private static void error(String msg, int code)
        {
            //exitCode = code;
            FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.ERROR, msg);
        }

        /**
         * Prints the warning message 'msg' to standard err, prepending "WARNING"
         * to it.
         *
         * @param msg The error message
         * */

        private static void warning(String msg)
        {
            FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.WARNING, msg);
        }

        private static string GetImageType(Stream inStream)
        {
            try
            {
                var bytes = new byte[2];
                inStream.Position = 0;
                inStream.Read(bytes, 0, 2);
                inStream.Position = 0;
                var imgType = Encoding.UTF8.GetString(bytes, 0, 2);
                return imgType;
            }
            catch
            {
                return null;
            }
        }
    }
}
