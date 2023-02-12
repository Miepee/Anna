using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AnnaLib;
using AnnaLib.XML;
using Eto.Forms;

namespace AnnaGUI
{
	// TODO: do not have heavy stuff on main ui thread
	// TODO: eventually split part stuff into a lib
	public partial class MainForm : Form
	{
		private string configFolder => CreateAndReturnPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/AnnaTool");
	
		private string packFolder => CreateAndReturnPath(configFolder + "/Packs");
		
		private static string CreateAndReturnPath(string path)
		{
			Directory.CreateDirectory(path);
			return path;
		}

		private string configFile => configFolder + "/Anna.xml";

		private string launcherDirectory;
		private string profilePath => launcherProfilesPath + "/" + profileDropdown.SelectedKey;
		private string launcherProfilesPath => launcherDirectory + "/Profiles";
		private string launcherPatchDataPath => launcherDirectory + "/PatchData";
		private string assetPath
		{ get
		{
			if (OS.IsWindows)
				return profilePath;
			if (OS.IsLinux)
			{
				if (File.Exists(profilePath + "/AM2R.AppImage"))
					return profilePath + "/squashfs-root/usr/bin/assets";
				
				return profilePath + "/assets";
			}
			//TODO: mac
			if (OS.IsMac)
			{
	            
			}
			return null;
		}
		}

		private const string toolFile = "_AnnaTool.txt";
		private const string originalOption = "Original";
		private const string packMusicFolder = "Music";
		private const string packPalettesFolder = "Palette";
		private const string packLangFolder = "Language";

		private bool unsavedProgress;
	
		private readonly CustomizeProfile currentProfile = new CustomizeProfile();
		private readonly Config Config = new Config();
		
		private void DirPickerClickEvent(object sender, EventArgs args)
		{
			var dialog = new SelectFolderDialog();
			if (dialog.ShowDialog(this) != DialogResult.Ok)
				return;
			
			launcherDirectory = dialog.Directory;

			#region Checks on whether dir is valid
			// Launcher directory has to have these folders
			string[] requiredFolders = new[] { "PatchData", "Profiles", "Mods" };
			bool areRequiredFoldersThere = true;
			foreach (string folder in requiredFolders)
			{
				if (Directory.Exists(launcherDirectory + "/" + folder))
					continue;
				
				areRequiredFoldersThere = false;
				break;
			}
			if (!areRequiredFoldersThere)
			{
				Application.Instance.Invoke(() =>
				{
					MessageBox.Show(this, "Invalid AM2RLauncher directory!", MessageBoxType.Error);
				});
				return;
			}
			
			// Determine if launcher OS matches current OS
			var launcherDataOS = Serializer.Deserialize<ProfileXML>(File.ReadAllText(launcherPatchDataPath + "/profile.xml")).OperatingSystem;
        
			// Too old version
			if (launcherDataOS is null)
			{
				Application.Instance.Invoke(() =>
				{
					MessageBox.Show(this, "Too old version of AM2RLauncher data. Please run the AM2RLauncher once with \"Automatically update AM2R\" enabled to update its data.", MessageBoxType.Error);
				});
				return;
			}

			if (launcherDataOS != OS.Name)
			{
				Application.Instance.Invoke(() =>
				{
					MessageBox.Show(this, "AM2RLauncher data is for an invalid Operating System. Please choose an AM2RLauncher data folder for your OS (" + OS.Name + ")", MessageBoxType.Error);
				});
				return;
			}
			#endregion

			SetLauncherDirectoryAndLoadProfiles();
		}
		
		private void ProfileDropDownSelectedIndexChanged(object sender, EventArgs args)
		{
			// TODO: warn on unsaved progress

			currentProfile.Profile = profileDropdown.SelectedValue as ProfileXML;
			InitializeAssetDropdown();
			unsavedProgress = false;
			if (currentProfile.Profile is not null)
				selectedProfileLabel.Text = currentProfile.Profile.Name + " v" + currentProfile.Profile.Version + "\nby " + currentProfile.Profile.Author;
		}
		
		private void InitializeAssetDropdown()
        {
            bool IsDirEmpty(DirectoryInfo dir)
            {
	            return dir.GetFiles().Length == 0 && dir.GetDirectories().Length == 0;
            }
            
            List<object> palettesList = new List<object> { originalOption };
            List<object> musicList = new List<object> { originalOption };
            List<object> languagesList = new List<object> { originalOption };
            foreach (DirectoryInfo packDir in new DirectoryInfo(packFolder).GetDirectories())
            {
	            foreach (var assetDir in packDir.GetDirectories())
	            {
		            if (IsDirEmpty(assetDir))
			            continue;
		            
		            if (assetDir.Name == packMusicFolder)
			            musicList.Add(packDir.Name);
		            else if(assetDir.Name == packPalettesFolder)
			            palettesList.Add(packDir.Name);
		            else if (assetDir.Name == packLangFolder)
			            languagesList.Add(packDir.Name);
	            }
            }

            paletteDropdown.DataStore = palettesList;
            musicDropdown.DataStore = musicList;
            langDropdown.DataStore = languagesList;
            
            if (File.Exists(assetPath + "/" + toolFile))
            {
	            var earlierProfile = Serializer.Deserialize<CustomizeProfile>(File.ReadAllText(assetPath + "/" + toolFile));
	            
	            paletteDropdown.SelectedIndex = paletteDropdown.DataStore.ToList().IndexOf(paletteDropdown.DataStore.FirstOrDefault(e => e.ToString() == earlierProfile.Palette));
	            musicDropdown.SelectedIndex = musicDropdown.DataStore.ToList().IndexOf(musicDropdown.DataStore.FirstOrDefault(e => e.ToString() == earlierProfile.Music));
	            langDropdown.SelectedIndex = langDropdown.DataStore.ToList().IndexOf(langDropdown.DataStore.FirstOrDefault(e => e.ToString() == earlierProfile.Language));
            }
            else 
            {
	            paletteDropdown.SelectedIndex = 0;
	            musicDropdown.SelectedIndex = 0;
	            langDropdown.SelectedIndex = 0;
            }
            currentProfile.Language = langDropdown.SelectedKey;
            currentProfile.Music = musicDropdown.SelectedKey;
            currentProfile.Palette = paletteDropdown.SelectedKey;
            ChangeDiffText();
        }

		private void ApplyButtonClickEvent(object sender, EventArgs args)
		{
			string currentMusic = musicDropdown.SelectedKey;
            string currentLang = langDropdown.SelectedKey;
            string currentPalette = paletteDropdown.SelectedKey;
            if (currentProfile.Language == currentLang && currentProfile.Music == currentMusic && currentProfile.Palette == currentPalette)
	            return;

            bool packAppImage = false;
            
            // extract appimage if it exists on Linux, do nothing otherwise
            if (OS.IsLinux)
            {
	            if (File.Exists(profilePath + "/AM2R.AppImage"))
	            {
		            packAppImage = true;
		            if (!Directory.Exists(profilePath + "/squashfs-root"))
		            {
			            using Process process = new Process();
			            process.StartInfo.FileName = profilePath + "/AM2R.AppImage";
			            process.StartInfo.Arguments = "--appimage-extract";
			            process.StartInfo.CreateNoWindow = false;
			            process.Start();
			            process.WaitForExit();
		            }
	            }
            }
            
            string origDataPath = "";
            if (currentProfile.Profile.Name == "Community Updates (Latest)")
            {
	            origDataPath = launcherPatchDataPath + "/data/files_to_copy";
            }
            else
            {
	            string prefix = launcherDirectory + "/Mods";
	            foreach (var folder in new DirectoryInfo(prefix).GetDirectories())
	            {
		            if (Serializer.Deserialize<ProfileXML>(File.ReadAllText(folder.FullName + "/profile.xml")).Name != currentProfile.Profile.Name)
			            continue;
		            
		            origDataPath = prefix + "/" + folder.Name + "/files_to_copy";
		            break;
	            }
            }

            // Change Music
            if (currentProfile.Music != currentMusic)
            {
	            if (currentMusic != originalOption)
	            {
		            // we only want to copy oggs, as otherwise people might place palette/lang/data.win files into the music place and do conflicting stuff
		            foreach (var file in new DirectoryInfo(packFolder + "/" + currentMusic + "/" + packMusicFolder).GetFiles().Where(f => f.Extension == ".ogg"))
			            file.CopyTo(assetPath + "/" + file.Name, true);
	            }
	            else
	            {
		            // first delete everything from the pack, then move old assets back in
		            DeleteLeftoverAssets(packFolder + "/" + currentProfile.Music + "/" + packMusicFolder, origDataPath, assetPath);

		            // Because music is fun, we first need to use 1.1's music, and then the one from the profile
		            // This will cause different files if one had HQ music installed earlier, but I dont care for that rn
		            var tempExtract = Path.GetTempPath() + "/" + Guid.NewGuid();
		            Directory.CreateDirectory(tempExtract);
		            // Only extract oggs from archive
		            using (ZipArchive archive = ZipFile.OpenRead(launcherDirectory + "/AM2R_11.zip"))
		            {
			            foreach (var entry in archive.Entries)
			            {
				            if (!entry.FullName.Contains('/') && entry.FullName.EndsWith(".ogg"))
					            entry.ExtractToFile(tempExtract + "/" + entry.Name, true);
			            }
		            }
		            // copy songs from 11
		            foreach (var ogg in new DirectoryInfo(tempExtract).GetFiles())
		            {
			            var name = ogg.Name;
			            if (currentProfile.Profile.OperatingSystem == "Linux" || currentProfile.Profile.OperatingSystem == "Mac")
				            name = ogg.Name.ToLower();
			            
			            ogg.MoveTo(assetPath + "/" + name, true);
		            }
					// copy songs from mod
					// files can take a while to copy, so i'm paralleizing them in hopes of making it slightly faster.
					var destinationPath = assetPath;
					Parallel.ForEach(new DirectoryInfo(origDataPath).EnumerateFiles(),
					                 (ogg) =>
					                 {
						                 if (!ogg.Extension.EndsWith(".ogg")) return;
						                 ogg.CopyTo(destinationPath + "/" + ogg.Name, true);
					                 });
	            }
            }
            
            // Change Palette
            if (currentProfile.Palette != currentPalette)
            {
	            if (currentPalette != originalOption)
	            {
		            DirectoryCopy(packFolder + "/" + currentPalette + "/" + packPalettesFolder, assetPath + "/mods/palettes");
	            }
	            else
	            {
		            // first delete everything from the pack, then move old assets back in
		            DeleteLeftoverAssets(packFolder + "/" + currentProfile.Palette + "/" + packPalettesFolder, origDataPath + "/mods/palettes", assetPath + "/mods/palettes");

		            DirectoryCopy(origDataPath + "/mods/palettes", assetPath + "/mods/palettes");
	            }
            }
            
            // Change Language
            if (currentProfile.Language != currentLang)
            {
	            if (currentLang != originalOption)
	            {
		            DirectoryCopy(packFolder + "/" + currentLang + "/" + packLangFolder, assetPath + "/lang");
	            }
	            else
	            {
		            // first delete everything from the pack, then move old assets back in
		            DeleteLeftoverAssets(packFolder + "/" + currentProfile.Language + "/" + packLangFolder, origDataPath + "/lang", assetPath + "/lang");
		            DirectoryCopy(origDataPath + "/lang", assetPath + "/lang");
	            }
            }

            currentProfile.Music = currentMusic;
            currentProfile.Palette = currentPalette;
            currentProfile.Language = currentLang;

            File.WriteAllText(assetPath + "/" + toolFile, Serializer.Serialize<CustomizeProfile>(currentProfile));

            // If linux non-native, pack appimage back in
            if (packAppImage)
            {
	            using Process process = new Process();
	            process.StartInfo.FileName = launcherPatchDataPath + "/utilities/appimagetool-x86_64.AppImage";
	            process.StartInfo.Arguments = "-n \"" + profilePath + "/squashfs-root\" \"" + profilePath + "/AM2R.AppImage\"";
	            process.StartInfo.EnvironmentVariables.Add("ARCH", "x86_64");
	            process.StartInfo.CreateNoWindow = false;
	            process.Start();
	            process.WaitForExit();
            }
            
            ChangeDiffText();
            Application.Instance.Invoke(() =>
            {
	            MessageBox.Show(this, "Applied successfully.");
            });
		}
		
		private void AssetSelectedIndexChanged()
		{
			unsavedProgress = true;
			ChangeDiffText();
		}

		private void ChangeDiffText()
		{
			diffLabel.Text = "Changes:\n" +
			                 "Music: " + currentProfile.Music + " -> " + musicDropdown.SelectedKey + "\n" +
			                 "Palette: " + currentProfile.Palette + " -> " + paletteDropdown.SelectedKey + "\n" +
			                 "Language: " + currentProfile.Language + " -> " + langDropdown.SelectedKey + "\n";
		}

		private void MainFormClosing(object sender, EventArgs args)
		{
			File.WriteAllText(configFile, Serializer.Serialize<Config>(Config));
		}

		private void SetLauncherDirectoryAndLoadProfiles()
		{
			dirTextBox.Text = launcherDirectory;
			Config.LastLauncherPath = launcherDirectory;

			List<ProfileXML> lists = new List<ProfileXML>();
			foreach (var dir in new DirectoryInfo(launcherProfilesPath).GetDirectories())
			{
				var profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(dir.FullName + "/profile.xml"));
				if (profile.Installable && dir.Name == profile.Name)
					lists.Add(profile);
			}

			profileDropdown.DataStore = lists.OrderBy(e => e.Name);
			profileDropdown.SelectedKeyBinding.Bind(() => (profileDropdown.SelectedValue as DirectoryInfo)?.Name);
			profileDropdown.SelectedIndex = 0;
		}
		
		private void DeleteLeftoverAssets(string mainDir, string origAssetDir, string newAssetDir)
		{
			void RecursivelyDeleteNewAssets(DirectoryInfo recursiveDir)
			{
				if (!Directory.Exists(recursiveDir.FullName.Replace(mainDir, origAssetDir)))
				{
					recursiveDir.Delete();
					return;
				}

				foreach (var file in recursiveDir.GetFiles())
				{
					if (!File.Exists(file.FullName.Replace(mainDir, origAssetDir)))
						File.Delete(file.FullName.Replace(mainDir, newAssetDir));
				}

				foreach (var subDir in recursiveDir.GetDirectories())
				{
					RecursivelyDeleteNewAssets(subDir);
				}
			}
			
			RecursivelyDeleteNewAssets(new DirectoryInfo(mainDir));
		}
		
		public static void DirectoryCopy(string sourceDirName, string destDirName, bool overwriteFiles = true, bool copySubDirs = true)
		{
			// Get the subdirectories for the specified directory.
			DirectoryInfo dir = new DirectoryInfo(sourceDirName);

			if (!dir.Exists)
			{
				throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
			}

			DirectoryInfo[] dirs = dir.GetDirectories();

			// If the destination directory doesn't exist, create it.
			Directory.CreateDirectory(destDirName);

			// Get the files in the directory and copy them to the new location.
			FileInfo[] files = dir.GetFiles();
			foreach (FileInfo file in files)
			{
				string tempPath = Path.Combine(destDirName, file.Name);
				file.CopyTo(tempPath, overwriteFiles);
			}

			// If copying subdirectories, copy them and their contents to new location.
			if (!copySubDirs)
				return;

			foreach (DirectoryInfo subDir in dirs)
			{
				string tempPath = Path.Combine(destDirName, subDir.Name);
				DirectoryCopy(subDir.FullName, tempPath, overwriteFiles);
			}

		}
		
		/// <summary>
		/// Recursively lowercases all files and folders from a specified directory.
		/// </summary>
		/// <param name="directory">The path to the directory whose contents should be lowercased.</param>
		public static void LowercaseFolder(string directory)
		{
			DirectoryInfo dir = new DirectoryInfo(directory);

			foreach(var file in dir.GetFiles())
			{
				if (file.Name == file.Name.ToLower()) continue;
				// Windows is dumb, thus we need to move in two trips
				file.MoveTo(file.DirectoryName + "/" + file.Name.ToLower() + "_");
				string newPath = file.FullName.Substring(0, file.FullName.Length - 1);
				File.Delete(newPath);
				file.MoveTo(newPath);
			}

			foreach(var subDir in dir.GetDirectories())
			{
				if (subDir.Name == subDir.Name.ToLower()) continue;
				// ReSharper disable once PossibleNullReferenceException - since this is a subdirectory, it always has a parent
				// Windows is dumb, thus we need to move in two trips
				subDir.MoveTo(subDir.Parent.FullName + "/" + subDir.Name.ToLower() + "_");
				// -2 because after a moving operation, DirInfo already appends a / 
				subDir.MoveTo(subDir.FullName.Substring(0, subDir.FullName.Length-2));
				LowercaseFolder(subDir.FullName);
			}
		}
		
		private void AddPackButtonOnClick(object sender, EventArgs e)
		{
			var picker = new OpenFileDialog() { Filters = { new FileFilter("Asset Pack for Anna", "*.apa") } };
			picker.Title = "Select Asset Pack file";
			if (picker.ShowDialog(this) != DialogResult.Ok)
				return;

			if (Directory.Exists(packFolder + "/" + Path.GetFileNameWithoutExtension(picker.FileName)))
			{
				Application.Instance.Invoke(() =>
				{
					MessageBox.Show(this, "Pack name is in use already. Please either update the existing pack, or rename the Pack file.", MessageBoxType.Error);
				});
				return;
			}
			ZipFile.ExtractToDirectory(picker.FileName, packFolder + "/" + Path.GetFileNameWithoutExtension(picker.FileName));
			if (OS.IsUnix) LowercaseFolder(packFolder + "/" + Path.GetFileNameWithoutExtension(picker.FileName));
			
			ScanConfigForPacks();
		}
		private void UpdatePackButtonOnClick(object sender, EventArgs e)
		{
			var picker = new OpenFileDialog() { Filters = { new FileFilter("Asset Pack for Anna", "*.apa") } };
			picker.Title = "Select Asset Pack file";
			if (picker.ShowDialog(this) != DialogResult.Ok)
				return;

			string name = packDropdown.SelectedKey;
			int index = packDropdown.SelectedIndex;
			Directory.Delete(packFolder + "/" + packDropdown.SelectedKey, true);
			ZipFile.ExtractToDirectory(picker.FileName, packFolder + "/" + name);
			packDropdown.SelectedIndex = index;
			
			ScanConfigForPacks();
		}
		private void RemovePackButtonOnClick(object sender, EventArgs e)
		{
			DialogResult result = DialogResult.No;
			Application.Instance.Invoke(() =>
			{
				result = MessageBox.Show(this, "Do you really want to delete \"" + packDropdown.SelectedKey + "\"?", MessageBoxButtons.YesNo, MessageBoxType.Question);
			});
			if (result != DialogResult.Yes)
				return;
			
			Directory.Delete(packFolder + "/" + packDropdown.SelectedKey, true);
			packDropdown.SelectedIndex = -1;
			ScanConfigForPacks();
			if (packDropdown.DataStore.Any())
				packDropdown.SelectedIndex = 0;
		}

		private void ScanConfigForPacks()
		{
			packDropdown.DataStore = new DirectoryInfo(packFolder).GetDirectories().Where(d => d.GetDirectories().Length != 0).Select(d => d.Name).OrderBy(d => d);
		}
	}
}
