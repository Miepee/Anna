using System;
using System.IO;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using AnnaLib;
using AnnaLib.XML;

namespace AnnaGUI;
public partial class MainForm : Form
{
	public MainForm()
 	{
 		Title = "Anna v" + Core.Version;
 		MinimumSize = new Size(200, 200);
        ClientSize = new Size(300, 600);
        Icon = new Icon(1f, new Bitmap(Resources.Icon));

        var scrollable = new Scrollable() {Border = BorderType.None};
        var applyLayout = new DynamicLayout() { Padding = 15, Spacing = new Size(10, 10), };
        var configLayout = new DynamicLayout() { Padding = 15, Spacing = new Size(10, 10), };

        // apply tab
        var launcherDirPanel = new DynamicLayout();
        launcherDirPanel.AddRow(dirLabel, null, dirButton);

        var launcherTextPanel = new DynamicLayout();
        launcherTextPanel.Add(dirTextBox);

        var profilePanel = new DynamicLayout();
        profilePanel.AddRow(profileLabel);
        profilePanel.AddRow(profileDropdown);
        profilePanel.AddCentered(selectedProfileLabel);

        var assetsPanel = new DynamicLayout() { Spacing = new Size(5, 5)};
        assetsPanel.AddRow(musicLabel);
        assetsPanel.AddRow(musicDropdown);
        assetsPanel.AddSpace();
        assetsPanel.AddRow(palettesLabel);
        assetsPanel.AddRow(paletteDropdown);
        assetsPanel.AddSpace();
        assetsPanel.AddRow(langLabel);
        assetsPanel.AddRow(langDropdown);
        assetsPanel.AddSpace();
        var diffPanel = new DynamicLayout();
        diffPanel.AddCentered(diffLabel);

        var applyPanel = new DynamicLayout();
        applyPanel.Add(applyButton);
        // TODO: button to reset everything to ORIGINAL
        // TODO: change between individual changing and full pack changing 
        // TODO: texture pages, code patches

        applyLayout.AddRange(launcherDirPanel, launcherTextPanel, profilePanel, assetsPanel, diffPanel, null, applyPanel);

        // config tab
        configLayout.AddRow(addPackButton);
        configLayout.AddRow(new Label() {Height = 15});
        configLayout.AddRow(packLabel);
        configLayout.AddRow(packDropdown);
        configLayout.AddRow(updatePackButton);
        configLayout.AddRow(removePackButton);
        configLayout.AddSpace();
        
        var applyPage = new TabPage()
        {
	        Text = "Apply Packs",
	        Content = applyLayout
        };

        var configPage = new TabPage()
        {
	        Text = "Pack Configuration",
	        Content = configLayout
        };
        
        scrollable.Content = new TabControl()
        {
	        Pages =
	        {
		        applyPage,
		        configPage
	        }
        };

        dirButton.Click += DirPickerClickEvent;
        profileDropdown.SelectedIndexChanged += ProfileDropDownSelectedIndexChanged;
        musicDropdown.SelectedIndexChanged += (_, _) => AssetSelectedIndexChanged();
        paletteDropdown.SelectedIndexChanged += (_, _) => AssetSelectedIndexChanged();
        langDropdown.SelectedIndexChanged += (_, _) => AssetSelectedIndexChanged();
        applyButton.Click += ApplyButtonClickEvent;
        this.Closing += MainFormClosing;

        addPackButton.Click += AddPackButtonOnClick;
        updatePackButton.Click += UpdatePackButtonOnClick;
        removePackButton.Click += RemovePackButtonOnClick;

		Content = scrollable;

		ScanConfigForPacks();
		if (packDropdown.DataStore.Any())
			packDropdown.SelectedIndex = 0;
        if (File.Exists(configFile))
        {
	        Config = Serializer.Deserialize<Config>(File.ReadAllText(configFile));
	        launcherDirectory = Config.LastLauncherPath;
	        SetLauncherDirectoryAndLoadProfiles();
        }
    }
	Label dirLabel = new Label() { Text = "Launcher Directory:" };
	Button dirButton = new Button() {Text = "Select Folder"};
	TextBox dirTextBox = new TextBox() { ReadOnly = true};

	Label profileLabel = new Label() { Text = "Profile:" };
	DropDown profileDropdown = new DropDown();
	Label selectedProfileLabel = new Label() {TextAlignment = TextAlignment.Center};
	
	Label musicLabel = new Label() { Text = "Music:" };
	DropDown musicDropdown = new DropDown();

	Label palettesLabel = new Label() { Text = "Palette:" };
	DropDown paletteDropdown = new DropDown();

	Label langLabel = new Label() { Text = "Language:" };
	DropDown langDropdown = new DropDown();

	Label diffLabel = new Label();

	Button applyButton = new Button() { Text = "Apply" };

	Button addPackButton = new Button() { Text = "Add Pack" };
	Label packLabel = new Label() { Text = "Currently installed Packs:"};
	DropDown packDropdown = new DropDown();
	Button removePackButton = new Button() { Text = "Remove Pack" };
	Button updatePackButton = new Button() { Text = "Update Pack" };
}