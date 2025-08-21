
namespace LibreMetaverse.RLV.Tests
{
    public class SampleInventoryTree
    {
        public RlvSharedFolder Root { get; set; } = null!;

        public RlvSharedFolder Clothing_Folder { get; set; } = null!;
        public RlvSharedFolder Clothing_Hats_Folder { get; set; } = null!;
        public RlvSharedFolder Clothing_Hats_SubHats_Folder { get; set; } = null!;
        public RlvSharedFolder Accessories_Folder { get; set; } = null!;


        public RlvInventoryItem Root_Clothing_Hats_FancyHat_Chin { get; set; } = null!;
        public RlvInventoryItem Root_Clothing_Hats_PartyHat_Spine { get; set; } = null!;
        public RlvInventoryItem Root_Clothing_BusinessPants_Pelvis { get; set; } = null!;
        public RlvInventoryItem Root_Clothing_RetroPants { get; set; } = null!;
        public RlvInventoryItem Root_Clothing_HappyShirt { get; set; } = null!;
        public RlvInventoryItem Root_Accessories_Glasses { get; set; } = null!;
        public RlvInventoryItem Root_Accessories_Watch { get; set; } = null!;

        public static SampleInventoryTree BuildInventoryTree()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \-Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var root = new RlvSharedFolder(new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), "#RLV");
            var clothing_folder = root.AddChild(new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"), "Clothing");
            var clothing_hats_folder = clothing_folder.AddChild(new Guid("dddddddd-dddd-4ddd-8ddd-dddddddddddd"), "Hats");
            var clothing_hats_subhats_folder = clothing_hats_folder.AddChild(new Guid("ffffffff-0000-4000-8000-000000000000"), "Sub Hats");
            var privateTree = root.AddChild(new Guid("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"), ".private");
            var accessories_folder = root.AddChild(new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"), "Accessories");

            var accessories_watch = accessories_folder.AddItem(new Guid("c0000000-cccc-4ccc-8ccc-cccccccccccc"), "Watch", null, null, null);
            var accessories_glasses = accessories_folder.AddItem(new Guid("c1111111-cccc-4ccc-8ccc-cccccccccccc"), "Glasses", null, null, null);

            var clothing_businessPants_pelvis = clothing_folder.AddItem(new Guid("b0000000-bbbb-4bbb-8bbb-bbbbbbbbbbbb"), "Business Pants (Pelvis)", null, null, null);
            var clothing_happyShirt = clothing_folder.AddItem(new Guid("b1111111-bbbb-4bbb-8bbb-bbbbbbbbbbbb"), "Happy Shirt", null, null, null);
            var clothing_retroPants = clothing_folder.AddItem(new Guid("b2222222-bbbb-4bbb-8bbb-bbbbbbbbbbbb"), "Retro Pants", null, null, null);

            var clothing_hats_partyHat_spine = clothing_hats_folder.AddItem(new Guid("d0000000-dddd-4ddd-8ddd-dddddddddddd"), "Party Hat (Spine)", null, null, null);
            var clothing_hats_fancyHat_chin = clothing_hats_folder.AddItem(new Guid("d1111111-dddd-4ddd-8ddd-dddddddddddd"), "Fancy Hat (chin)", null, null, null);

            return new SampleInventoryTree()
            {
                Root = root,
                Clothing_Folder = clothing_folder,
                Accessories_Folder = accessories_folder,
                Clothing_Hats_Folder = clothing_hats_folder,
                Clothing_Hats_SubHats_Folder = clothing_hats_subhats_folder,
                Root_Clothing_Hats_PartyHat_Spine = clothing_hats_partyHat_spine,
                Root_Clothing_Hats_FancyHat_Chin = clothing_hats_fancyHat_chin,
                Root_Accessories_Glasses = accessories_glasses,
                Root_Clothing_BusinessPants_Pelvis = clothing_businessPants_pelvis,
                Root_Clothing_HappyShirt = clothing_happyShirt,
                Root_Clothing_RetroPants = clothing_retroPants,
                Root_Accessories_Watch = accessories_watch
            };
        }
    }
}
