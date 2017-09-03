using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace CSJ2K.Util
{
    internal class StoreFileInfo : IFileInfo
	{
		#region CONSTRUCTORS

		internal StoreFileInfo(string fileName)
		{
			FullName = fileName;
		}

		#endregion

		#region PROPERTIES

		public string Name { get { return Path.GetFileName(FullName); } }

		public string FullName { get; private set; }

	    public bool Exists
	    {
			get
			{
				return Task.Run(async () =>
					                      {
						                      try
						                      {
							                      var file = await StorageFile.GetFileFromPathAsync(FullName);
							                      return file != null;
						                      }
						                      catch
						                      {
							                      return false;
						                      }
					                      }).Result;
			}
	    }

	    #endregion

		#region METHODS

		public bool Delete()
		{
				return Task.Run(async () =>
					                      {
						                      try
						                      {
							                      var file = await StorageFile.GetFileFromPathAsync(FullName);
							                      await file.DeleteAsync();
							                      return true;
						                      }
						                      catch
						                      {
							                      return false;
						                      }
					                      }).Result;
		}
		
		#endregion
    }
}
