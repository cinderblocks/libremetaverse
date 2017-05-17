// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

namespace CSJ2K.Util
{
    using System;

    public static class FileInfoFactory
    {
        #region FIELDS

        private static IFileInfoCreator _creator;

        #endregion

        #region CONSTRUCTORS

        static FileInfoFactory()
        {
            _creator = J2kSetup.GetSinglePlatformInstance<IFileInfoCreator>();
        }

        #endregion

        #region METHODS

        public static void Register(IFileInfoCreator creator)
        {
            _creator = creator;
        }

        internal static IFileInfo New(string fileName)
        {
            if (_creator == null) throw new InvalidOperationException("No file info creator is registered.");
            if (fileName == null) throw new ArgumentNullException("fileName");

            return _creator.Create(fileName);
        }

        #endregion
    }
}