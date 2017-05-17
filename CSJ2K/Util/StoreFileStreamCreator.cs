// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

namespace CSJ2K.Util
{
    using System.IO;

    public class StoreFileStreamCreator : IFileStreamCreator
    {
        #region FIELDS

        private static readonly IFileStreamCreator Instance = new StoreFileStreamCreator();

        #endregion

        #region METHODS

        public static void Register()
        {
            FileStreamFactory.Register(Instance);
        }

        public Stream Create(string path, string mode)
        {
            return new StoreFileStream(path, mode);
        }

        #endregion
    }
}
