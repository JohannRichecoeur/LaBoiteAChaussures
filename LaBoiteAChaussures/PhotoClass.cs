using System.Collections.Generic;
using System.Linq;
using Windows.Storage;

namespace LaBoiteAChaussures
{
    public class PhotoClass
    {
        public string Name { get; set; }

        public string Path { get; set; }

        public static IEnumerable<PhotoClass> HydrateFromStorageFileList(IEnumerable<StorageFile> storageFileList)
        {
            return storageFileList.Select(HydrateFromStorageFile);
        }

        public static PhotoClass HydrateFromStorageFile(StorageFile storageFile)
        {
            return new PhotoClass()
            {
                Name = storageFile.Name,
                Path = storageFile.Path,
            };
        }
    }
}