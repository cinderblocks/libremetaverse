// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

namespace CSJ2K.Util
{
    public class StoreFileInfoCreator : IFileInfoCreator
    {
        #region FIELDS

        private static readonly IFileInfoCreator Instance = new StoreFileInfoCreator();

        #endregion

        #region METHODS

        public static void Register()
        {
            FileInfoFactory.Register(Instance);
        }

        public IFileInfo Create(string name)
        {
            return new StoreFileInfo(name);
        }

        #endregion
    }
}
