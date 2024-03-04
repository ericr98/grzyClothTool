﻿using CodeWalker;
using CodeWalker.GameFiles;
using grzyClothTool.Controls;
using grzyClothTool.Helpers;
using grzyClothTool.Models;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;
using UserControl = System.Windows.Controls.UserControl;

namespace grzyClothTool.Views
{
    /// <summary>
    /// Interaction logic for Project.xaml
    /// </summary>
    public partial class ProjectWindow : UserControl
    {
        private readonly AddonManager Addon;

        public ProjectWindow()
        {
            InitializeComponent();

            if(DesignerProperties.GetIsInDesignMode(this))
            {
                Addon = new AddonManager("design");
                DataContext = Addon;
                return;
            }

            Addon = MainWindow.Addon;
            DataContext = Addon;
        }

        private async void Add_DrawableFile(object sender, RoutedEventArgs e)
        {
            var btn = sender as CustomButton;
            var isMaleBtn = btn.Label.ToString().Equals("male", StringComparison.CurrentCultureIgnoreCase);
            e.Handled = true;

            OpenFileDialog files = new()
            {
                Multiselect = true,
                Filter = "Drawable files (*.ydd)|*.ydd"
            };

            if (files.ShowDialog() == true)
            {
                var timer = new Stopwatch();
                timer.Start();

                await Addon.AddDrawables(files.FileNames, isMaleBtn);

                timer.Stop();
                CustomMessageBox.Show($"Added drawables in {timer.Elapsed}");
            }
        }

        private async void Add_DrawableFolder(object sender, RoutedEventArgs e)
        {
            var btn = sender as CustomButton;
            var isMaleBtn = btn.Tag.ToString().Equals("male", StringComparison.CurrentCultureIgnoreCase);
            e.Handled = true;

            OpenFolderDialog folder = new()
            {
                Title = "Select a folder containing drawable files",
            };

            if (folder.ShowDialog() == true)
            {
                var timer = new Stopwatch();
                timer.Start();
                foreach (var fldr in folder.FolderNames)
                {
                    var files = Directory.GetFiles(fldr, "*.ydd", SearchOption.TopDirectoryOnly);
                    await Addon.AddDrawables(files, isMaleBtn);
                }

                timer.Stop();
                CustomMessageBox.Show($"Added drawables in {timer.Elapsed}");
            }
        }

        private void BuildResource_Btn(object sender, RoutedEventArgs e)
        {
            BuildWindow buildWindow = new()
            {
                Owner = Window.GetWindow(this)
            };
            buildWindow.ShowDialog();
        }

        private void Preview_Btn(object sender, RoutedEventArgs e)
        {
            if (CWHelper.CWForm == null || CWHelper.CWForm.IsDisposed)
            {
                CWHelper.CWForm = new CustomPedsForm();
                CWHelper.CWForm.FormClosed += CWForm_FormClosed;
            }

            if (Addon.SelectedDrawable != null)
            {
                var ydd = CreateYddFile(Addon.SelectedDrawable);
                CWHelper.CWForm.LoadedDrawable = ydd.Drawables.First();

                if (Addon.SelectedTexture != null)
                {
                    var ytd = CreateYtdFile(Addon.SelectedTexture);
                    CWHelper.CWForm.LoadedTexture = ytd.TextureDict;
                }
            }

            CWHelper.CWForm.Show();
            Addon.IsPreviewEnabled = true;
        }

        private void CWForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Addon.IsPreviewEnabled = false;
        }

        private void SelectedDrawable_Changed(object sender, EventArgs e)
        {
            var args = e as SelectionChangedEventArgs;
            args.Handled = true;

            if(args.AddedItems.Count == 0)
            {
                Addon.SelectedDrawable = null;
                return;
            }



            Addon.SelectedDrawable = (Models.Drawable)args.AddedItems[0];
            if(Addon.SelectedDrawable.Textures.Count > 0)
            {
                Addon.SelectedTexture = Addon.SelectedDrawable.Textures.First();
                SelDrawable.SelectedIndex = 0;
            }
            
            if (Addon.IsPreviewEnabled)
            {
                var ydd = CreateYddFile(Addon.SelectedDrawable);
                var ytd = CreateYtdFile(Addon.SelectedTexture);

                CWHelper.CWForm.LoadedDrawable = ydd.Drawables.First();
                CWHelper.CWForm.LoadedTexture = ytd.TextureDict;
                CWHelper.CWForm.Refresh();


                if(Addon.SelectedDrawable.EnableHairScale)
                {
                    CWHelper.CWForm.UpdateSelectedDrawable(ydd.Drawables.First(), ytd.TextureDict, "HairScale", Addon.SelectedDrawable.HairScaleValue);
                } 
                else if(Addon.SelectedDrawable.EnableHighHeels)
                {
                    CWHelper.CWForm.UpdateSelectedDrawable(ydd.Drawables.First(), ytd.TextureDict, "HighHeels", Addon.SelectedDrawable.HighHeelsValue);
                }
                else
                {
                    CWHelper.CWForm.UpdateSelectedDrawable(ydd.Drawables.First(), ytd.TextureDict, "", "");
                }
            }
        }

        private void SelectedDrawable_Updated(object sender, DrawableUpdatedArgs e)
        {
            if (!Addon.TriggerSelectedDrawableUpdatedEvent) return;
            if (!Addon.IsPreviewEnabled) { return; }
            if (Addon.SelectedDrawable is null) { return; }
            if (Addon.SelectedDrawable.Textures.Count == 0) { return; }

            Addon.SelectedTexture = Addon.SelectedDrawable.Textures.First();
            SelDrawable.SelectedIndex = 0;

            var ydd = CreateYddFile(Addon.SelectedDrawable);
            var ytd = CreateYtdFile(Addon.SelectedTexture);


            CWHelper.CWForm.UpdateSelectedDrawable(ydd.Drawables.First(), ytd.TextureDict, e.UpdatedName, e.Value);
        }

        private void SelectedDrawable_TextureChanged(object sender, EventArgs e)
        {
            var args = e as SelectionChangedEventArgs;
            args.Handled = true;

            if (args.AddedItems.Count == 0)
            {
                Addon.SelectedTexture = null;
                return;
            }

            Addon.SelectedTexture = (Models.GTexture)args.AddedItems[0];

            if (Addon.IsPreviewEnabled)
            {
                var ytd = CreateYtdFile(Addon.SelectedTexture);

                CWHelper.CWForm.LoadedTexture = ytd.TextureDict;
                CWHelper.CWForm.Refresh();
            }
        }

        private static YtdFile CreateYtdFile(Models.GTexture t)
        {
            byte[] data = File.ReadAllBytes(t.File.FullName);

            RpfFileEntry rpf = RpfFile.CreateResourceFileEntry(ref data, 0);
            var decompressedData = ResourceBuilder.Decompress(data);
            YtdFile ytd = RpfFile.GetFile<YtdFile>(rpf, decompressedData);
            ytd.Name = Path.GetFileNameWithoutExtension(t.DisplayName);

            return ytd;
        }

        private static YddFile CreateYddFile(Models.Drawable d)
        {
            byte[] data = File.ReadAllBytes(d.File.FullName);

            RpfFileEntry rpf = RpfFile.CreateResourceFileEntry(ref data, 0);
            var decompressedData = ResourceBuilder.Decompress(data);
            YddFile ydd = RpfFile.GetFile<YddFile>(rpf, decompressedData);
            var drawable = ydd.Drawables.First();
            drawable.Name = Path.GetFileNameWithoutExtension(d.Name);
            drawable.IsHairScaleEnabled = d.EnableHairScale;
            drawable.IsHighHeelsEnabled = d.EnableHighHeels;


            return ydd;
        }
    }
}