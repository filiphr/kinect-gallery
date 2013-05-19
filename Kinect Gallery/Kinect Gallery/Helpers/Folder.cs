using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using Kinect_Gallery.Properties;


namespace Kinect_Gallery.Helpers
{
    class Folder
    {
        public String FolderName { get; set; }
        public String FolderPath { get; set; }
        public String ImagePath { get; set; }

        public Folder(String folderName, String folderPath, String imagePath)
        {
            this.FolderName = folderName;
            this.FolderPath = folderPath;
            this.ImagePath = imagePath;
        }
    }


    class Folders : ObservableCollection<Folder>
    {
        public Folders()
        {
            String imagesPath = Settings.Default.ImagesPath;

            Add(new Folder("BCN", @"C:\Users\test\Pictures\BCN", imagesPath+"\\folder.jpg"));
            Add(new Folder("EBEC day 1", @"C:\Users\test\Pictures\EBEC day 1", imagesPath + "\\folder.jpg"));
            Add(new Folder("Wallpapers", @"C:\Users\test\Pictures\Wallpapers", imagesPath + "\\folder.jpg"));
            Add(new Folder("NG Belgrad", @"C:\Users\test\Pictures\NG Belgrad", imagesPath + "\\folder.jpg"));
            Add(new Folder("Otvaranje", @"C:\Users\test\Pictures\EBEC 2012 da se sredat\Otvaranje", imagesPath + "\\folder.jpg"));
            Add(new Folder("Team Design", @"C:\Users\test\Pictures\EBEC 2012 da se sredat\Team Design", imagesPath + "\\folder.jpg"));
        }
    }
}