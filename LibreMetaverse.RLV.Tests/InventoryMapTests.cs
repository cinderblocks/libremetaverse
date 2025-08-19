namespace LibreMetaverse.RLV.Tests
{
    public class InventoryMapTests
    {
        #region TryGetFolderFromPath
        [Fact]
        public void TryGetFolderFromPath_Normal()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.True(inventoryMap.TryGetFolderFromPath("Clothing/Hats", true, out var foundFolder));
            Assert.Equal(foundFolder, sampleTree.Clothing_Hats_Folder);
        }

        [Fact]
        public void TryGetFolderFromPath_EmptyPath()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.False(inventoryMap.TryGetFolderFromPath("", true, out var foundFolder));
            Assert.Null(foundFolder);
        }

        [Fact]
        public void TryGetFolderFromPath_FolderNameContainsForwardSlash()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clo/thing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "Clo/thing";

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.True(inventoryMap.TryGetFolderFromPath("Clo/thing/Hats", true, out var foundFolder));
            Assert.Equal(foundFolder, sampleTree.Clothing_Hats_Folder);
        }

        [Fact]
        public void TryGetFolderFromPath_FolderNameContainsForwardSlashes()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- /Clo//thing//
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- //h/ats/
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "/Clo//thing//";
            sampleTree.Clothing_Hats_Folder.Name = "//h/ats/";

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.True(inventoryMap.TryGetFolderFromPath($"{sampleTree.Clothing_Folder.Name}/{sampleTree.Clothing_Hats_Folder.Name}", true, out var foundFolder));
            Assert.Equal(foundFolder, sampleTree.Clothing_Hats_Folder);
        }

        [Fact]
        public void TryGetFolderFromPath_FolderNameWithSlashPrefix()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- /Clo//thing//
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- //h/ats/
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "/Clothing";

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.True(inventoryMap.TryGetFolderFromPath($"{sampleTree.Clothing_Folder.Name}", true, out var foundFolder));
            Assert.Equal(foundFolder, sampleTree.Clothing_Folder);
        }

        [Fact]
        public void TryGetFolderFromPath_FolderNameWithSlashSuffix()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- /Clo//thing//
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- //h/ats/
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "Clothing/";

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.True(inventoryMap.TryGetFolderFromPath($"{sampleTree.Clothing_Folder.Name}", true, out var foundFolder));
            Assert.Equal(foundFolder, sampleTree.Clothing_Folder);
        }

        [Fact]
        public void TryGetFolderFromPath_FolderNameWithSlashAffix()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- /Clo//thing//
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- //h/ats/
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "/Clothing/";

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.True(inventoryMap.TryGetFolderFromPath($"{sampleTree.Clothing_Folder.Name}", true, out var foundFolder));
            Assert.Equal(foundFolder, sampleTree.Clothing_Folder);
        }


        [Fact]
        public void TryGetFolderFromPath_ContendingFoldersWithSlashes()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing///
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- //h/ats/
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var contendingTree1 = sharedFolder.AddChild(new Guid("12345678-0001-4ddd-8ddd-dddddddddddd"), "Clothing");
            var contendingTree3 = sharedFolder.AddChild(new Guid("12345678-0002-4ddd-8ddd-dddddddddddd"), "+Clothing///");
            var contendingTree4 = sharedFolder.AddChild(new Guid("12345678-0003-4ddd-8ddd-dddddddddddd"), "+Clothing///");

            sampleTree.Clothing_Folder.Name = "Clothing///";
            sampleTree.Clothing_Hats_Folder.Name = "//h/ats/";

            var inventoryMap = new InventoryMap(sharedFolder);

            // We prefer the exact match of "Clothing///" over the not so exact match of "+Clothing///" since it's exactly what we're searching for
            Assert.True(inventoryMap.TryGetFolderFromPath($"{sampleTree.Clothing_Folder.Name}/{sampleTree.Clothing_Hats_Folder.Name}", true, out var foundFolder));
            Assert.Equal(foundFolder, sampleTree.Clothing_Hats_Folder);
        }

        [Fact]
        public void TryGetFolderFromPath_InvalidPath()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.False(inventoryMap.TryGetFolderFromPath("Clothing/Hats123", true, out var foundFolder));
        }

        [Fact]
        public void TryGetFolderFromPath_IgnoreFolderPrefix()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- ~Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- +Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "~Clothing";
            sampleTree.Clothing_Hats_Folder.Name = "+Hats";

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.True(inventoryMap.TryGetFolderFromPath("Clothing/Hats", true, out var foundFolder));
            Assert.Equal(foundFolder, sampleTree.Clothing_Hats_Folder);
        }

        [Fact]
        public void TryGetFolderFromPath_FailOnHiddenFolder()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- .Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = ".Clothing";

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.False(inventoryMap.TryGetFolderFromPath(".Clothing", true, out var foundFolder));
        }

        [Fact]
        public void TryGetFolderFromPath_AllowHiddenFolder()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- .Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = ".Clothing";

            var inventoryMap = new InventoryMap(sharedFolder);

            Assert.True(inventoryMap.TryGetFolderFromPath(".Clothing", false, out var foundFolder));
            Assert.Equal(sampleTree.Clothing_Folder, foundFolder);
        }

        #endregion

        #region FindFoldersContaining
        [Fact]
        public void FindFoldersContaining_ById()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder);

            var actual = inventoryMap.FindFoldersContaining(false, sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId, null, null);

            var expected = new[] {
                sampleTree.Clothing_Hats_Folder
            };

            Assert.Equal(expected.OrderBy(n => n.Id), actual.OrderBy(n => n.Id));
        }

        [Fact]
        public void FindFoldersContaining_ByAttachmentType()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (Attached to chin)
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (Attached to chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder);

            var actual = inventoryMap.FindFoldersContaining(false, null, RlvAttachmentPoint.Chin, null);

            var expected = new[] {
                sampleTree.Clothing_Folder,
                sampleTree.Clothing_Hats_Folder
            };

            Assert.Equal(expected.OrderBy(n => n.Id), actual.OrderBy(n => n.Id));
        }

        [Fact]
        public void FindFoldersContaining_ByAttachmentType_SingleResult()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (Attached to chin)
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (Attached to chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder);

            // What this returns doesn't seem to be really defined. 'return single result' is deprecated since there can be multiple results nowadays
            var actual = inventoryMap.FindFoldersContaining(true, null, RlvAttachmentPoint.Chin, null);

            var expected = new[] {
                sampleTree.Clothing_Hats_Folder,
            };

            Assert.Equal(expected.OrderBy(n => n.Id), actual.OrderBy(n => n.Id));
        }

        [Fact]
        public void FindFoldersContaining_ByWearType()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (Worn as pants)
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (Worn as Hair)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Hair;

            var inventoryMap = new InventoryMap(sharedFolder);

            var actual = inventoryMap.FindFoldersContaining(false, null, null, RlvWearableType.Pants);

            var expected = new[] {
                sampleTree.Clothing_Folder
            };

            Assert.Equal(expected.OrderBy(n => n.Id), actual.OrderBy(n => n.Id));
        }
        #endregion
    }
}
