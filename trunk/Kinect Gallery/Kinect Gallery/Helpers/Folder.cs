using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using Kinect_Gallery.Properties;
using System.IO;


namespace Kinect_Gallery.Helpers
{
    class Folder
    {
        public String FolderName { get; set; }
        public String FolderPath { get; set; }
        public Uri ImagePath { get; set; }

        public Folder(String folderPath)
        {
            this.FolderName = System.IO.Path.GetFileName(folderPath);
            this.FolderPath = folderPath;
            this.ImagePath = getFirstImageForFolder(folderPath);
        }



        internal static Uri getFirstImageForFolder(string folderPath)
        {
            DirectoryInfo di = new DirectoryInfo(folderPath);
            FileInfo imagePath = di.GetFiles("*.jpg", SearchOption.AllDirectories).FirstOrDefault();
//            FileInfo imagePath = null;
            Uri imageUri = null;
            if (imagePath != null)
            {
                imageUri = new Uri(imagePath.FullName);
            }
            else {
                imageUri = new Uri(@"Images/folder.jpg", UriKind.Relative);
            }
            return imageUri;
        }
    }


    class Folders : ObservableCollection<Folder>
    {
        public Folders()
        {
            String imagesPath = Settings.Default.ImagesPath;
            String databasePath = Settings.Default.DatabasePath;
            // Read sample data from CSV file
            using (CsvFileReader reader = new CsvFileReader(databasePath + "\\Folders.csv"))
            {
                CsvRow row = new CsvRow();
                while (reader.ReadRow(row))
                {
                    string folderPath = row[0];
                    Add(new Folder(folderPath));
                }


                //Add(new Folder("BCN", @"C:\Users\test\Pictures\BCN", imagesPath+"\\folder.jpg"));
                //Add(new Folder("EBEC day 1", @"C:\Users\test\Pictures\EBEC day 1", imagesPath + "\\folder.jpg"));
                //Add(new Folder("Wallpapers", @"C:\Users\test\Pictures\Wallpapers", imagesPath + "\\folder.jpg"));
                //Add(new Folder("NG Belgrad", @"C:\Users\test\Pictures\NG Belgrad", imagesPath + "\\folder.jpg"));
                //Add(new Folder("Otvaranje", @"C:\Users\test\Pictures\EBEC 2012 da se sredat\Otvaranje", imagesPath + "\\folder.jpg"));
                //Add(new Folder("Team Design", @"C:\Users\test\Pictures\EBEC 2012 da se sredat\Team Design", imagesPath + "\\folder.jpg"));
            }
          //  Add(new Folder("Corsa", @"C:\Dopolnitelno\Sliki\Corsa", @"C:\Dopolnitelno\Projects\Visual Studio 2010\Projects\SlideshowGestures-WPF\Images\folder.jpg"));
        }
    }
}