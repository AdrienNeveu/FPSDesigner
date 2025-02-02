﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Media.Animation;
using FirstFloor.ModernUI.Windows.Controls;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using Microsoft.Win32;

using ModelViewer;

namespace Software.Pages
{
    /// <summary>
    /// Interaction logic for HandsTextures.xaml
    /// </summary>
    public partial class HandsTextures : UserControl
    {
        public HandsTextures()
        {
            InitializeComponent();

            selectButton.Click += selectButton_Click;

            string[] imageExt = new string[] { ".png", ".jpg", ".jpeg", ".gif", ".tga", ".raw", ".bmp", ".dds" };

            DirectoryInfo root = new DirectoryInfo(GlobalVars.rootProjectFolder + GlobalVars.contentRootFolder + Path.GetDirectoryName(GlobalVars.gameInfo.SpawnInfo.HandTexture));

            if (root.Exists)
            {
                FileInfo[] filesFound = root.GetFiles(Path.GetFileName(GlobalVars.gameInfo.SpawnInfo.HandTexture) + ".*");
                if(filesFound.Length > 0 && imageExt.Contains(filesFound[0].Extension))
                    imagePH.Source = new BitmapImage(new Uri(filesFound[0].FullName, UriKind.Absolute));
            }
        }

        void selectButton_Click(object sender, RoutedEventArgs e)
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog imageDialog = new OpenFileDialog();

            // Set filter options and filter index.
            imageDialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tga";
            imageDialog.FilterIndex = 1;

            imageDialog.Multiselect = false;

            // Process input if the user clicked OK.
            if (imageDialog.ShowDialog() == true)
            {
                // Copy file to texture path
                string destImage = GlobalVars.rootProjectFolder + GlobalVars.contentRootFolder + "/Textures/" + Path.GetFileName(imageDialog.FileName);
                System.IO.File.Copy(imageDialog.FileName, destImage, true);
                destImage = "Textures/" + Path.GetFileName(destImage);
                GlobalVars.gameInfo.SpawnInfo.HandTexture = destImage;
                GlobalVars.embeddedGame.WPFHandler("setElementInfo", new object[] { "handsTexture", GlobalVars.gameInfo.SpawnInfo.HandTexture });

                // Change image placeholder to this image
                imagePH.Source = new BitmapImage(new Uri(imageDialog.FileName));
            }
        }

    }
}
