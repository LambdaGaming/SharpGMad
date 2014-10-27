﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SharpGMad
{
    partial class Main : Form
    {
        /// <summary>
        /// The currently open addon.
        /// </summary>
        RealtimeAddon AddonHandle;

        private Main()
        {
            InitializeComponent();
            InitializeIcons();
            UnloadAddon();

            // Hook the lists to the icons
            tvFolders.ImageList = imgIconsSmall;
            lstFiles.SmallImageList = imgIconsSmall;
            lstFiles.LargeImageList = imgIconsLarge;

            tsmiViewElements_changeView(tsmiViewLargeIcons, new EventArgs()); // Default to large icons view.

            // Default to showing subfolders in file list.
            tsmiViewShowSubfolders.Checked = false;
            tsmiViewShowSubfolders_Click(tsmiViewShowSubfolders, new EventArgs()); // _Click() will set it to true.

            tsbCreateAddon.Enabled = !Program.WhitelistOverridden;
        }

        public Main(string[] args)
            : this()
        {
            // Try to autoload the addon if there's a first parameter specified.
            // This supports drag&dropping an addon file onto the EXE in Explorer.

            string path = String.Join(" ", args);
            if (path != String.Empty)
            {
                try
                {
                    LoadAddon(path);
                }
                catch (IOException)
                {
                    return;
                }
            }
        }

        // Visual Studio tends to crap up the good-looking icons when a saved project is reloaded...
        // So we don't rely on it, instead, we "runtime-create" the image lists.
        private ImageList imgIconsLarge;
        private ImageList imgIconsSmall;

        private void InitializeIcons()
        {
            // Form icon itself
            this.Icon = global::SharpGMad.Properties.Resources.gma_ico;

            // Large icons
            imgIconsLarge = new ImageList();
            imgIconsLarge.ColorDepth = ColorDepth.Depth32Bit;
            imgIconsLarge.TransparentColor = Color.Transparent;
            imgIconsLarge.ImageSize = new Size(32, 32);

            imgIconsLarge.Images.Add(global::SharpGMad.Properties.Resources.gma_32x32);
            imgIconsLarge.Images.Add(global::SharpGMad.Properties.Resources.file_32x32);
            imgIconsLarge.Images.Add(global::SharpGMad.Properties.Resources.folder_32x32);
            imgIconsLarge.Images.Add(global::SharpGMad.Properties.Resources.emptyfolder_32x32);

            imgIconsLarge.Images.SetKeyName(0, "gma");
            imgIconsLarge.Images.SetKeyName(1, "file");
            imgIconsLarge.Images.SetKeyName(2, "folder");
            imgIconsLarge.Images.SetKeyName(3, "emptyFolder");

            // Small icons
            imgIconsSmall = new ImageList();
            imgIconsSmall.ColorDepth = ColorDepth.Depth32Bit;
            imgIconsSmall.TransparentColor = Color.Transparent;
            imgIconsSmall.ImageSize = new Size(16, 16);

            imgIconsSmall.Images.Add(global::SharpGMad.Properties.Resources.gma_16x16);
            imgIconsSmall.Images.Add(global::SharpGMad.Properties.Resources.file_16x16);
            imgIconsSmall.Images.Add(global::SharpGMad.Properties.Resources.folder_16x16);
            imgIconsSmall.Images.Add(global::SharpGMad.Properties.Resources.emptyfolder_16x16);

            imgIconsSmall.Images.SetKeyName(0, "gma");
            imgIconsSmall.Images.SetKeyName(1, "file");
            imgIconsSmall.Images.SetKeyName(2, "folder");
            imgIconsSmall.Images.SetKeyName(3, "emptyFolder");
        }

        private void tsbOpenAddon_Click(object sender, EventArgs e)
        {
            DialogResult dropChanges = new DialogResult();
            if (AddonHandle is RealtimeAddon && AddonHandle.Modified)
            {
                dropChanges = MessageBox.Show("Do you want to open another addon without saving the current first?",
                    "An addon is already open", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            }

            if (dropChanges == DialogResult.Yes || 
                AddonHandle == null || (AddonHandle is RealtimeAddon && !AddonHandle.Modified))
            {
                UnloadAddon();
                DialogResult file = ofdAddon.ShowDialog();

                if (file == DialogResult.OK)
                    LoadAddon(ofdAddon.FileName);
            }
        }

        /// <summary>
        /// Loads an addon from the filesystem.
        /// </summary>
        /// <param name="filename">The path of the addon to load.</param>
        private void LoadAddon(string path)
        {
            try
            {
                AddonHandle = RealtimeAddon.Load(path, !FileExtensions.CanWrite(path));

                if (!AddonHandle.CanWrite)
                {
                    DialogResult openReadOnly = MessageBox.Show("This addon is locked by another process, " +
                        "and cannot be written.\n\n" +
                        "Would you like to open it in read-only mode?\nAll modification options will be disabled.",
                        "Addon locked", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (openReadOnly != DialogResult.Yes)
                    {
                        AddonHandle.Close();
                        return;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (IgnoredException e)
            {
                MessageBox.Show(e.Message, "Addon is corrupted", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (WhitelistException e)
            {
                if (!Program.WhitelistOverridden)
                {
                    DialogResult ovrride = MessageBox.Show("This addon is against the GMA whitelist rules defined by garry!\n" +
                        e.Message + "\n\nFor datamining purposes, it is still possible to open this addon, HOWEVER " +
                        "opening this addon is an illegal operation and SharpGMad will prevent further modifications.\n\n" +
                        "Do you want to enable forced opening of addons by overriding the whitelist?",
                        "Addon is corrupted", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (ovrride == DialogResult.No)
                    {
                        MessageBox.Show(e.Message, "Addon is corrupted", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        return;
                    }
                    else if (ovrride == DialogResult.Yes)
                    {
                        Program.WhitelistOverridden = true;
                        tsbCreateAddon.Enabled = !Program.WhitelistOverridden;
                        MessageBox.Show("Restrictions disabled.\nPlease browse for the addon to open it again.",
                            "Addon is corrupted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Text = "! - SharpGMad";
                        UpdateStatus("Restrictions disabled by user's request.");
                        return;
                    }
                }
            }
            catch (ArgumentException e)
            {
                MessageBox.Show(e.Message, "Addon is corrupted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (IOException ex)
            {
                MessageBox.Show("Unable to load the addon.\nAn exception happened.\n\n" + ex.Message, "Addon reading error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (ReaderException ex)
            {
                MessageBox.Show("There was an error parsing the file.\n\n" + ex.Message, "Addon reading error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (AddonHandle is RealtimeAddon)
            {
                UpdateMetadataPanel();
                UpdateFolderList();
                UpdateFileList();
                UpdateModified();
                UpdateStatus("Loaded the addon" + (AddonHandle.CanWrite ? null : " (read-only mode)"));

                tsbAddFile.Enabled = AddonHandle.CanWrite && !Program.WhitelistOverridden;
                tsbUpdateMetadata.Enabled = AddonHandle.CanWrite && !Program.WhitelistOverridden;
            }
        }

        /// <summary>
        /// Updates the form so it reflects the modified or not modified state.
        /// </summary>
        private void UpdateModified()
        {
            // Invoke the method if it was called from a thread which is not the thread Main was created in.
            //
            // Prevents the exception:
            // Cross-thread operation not valid: Control 'Main' accessed from a
            // thread other than the thread it was created on.
            if (lstFiles.InvokeRequired)
                this.Invoke((MethodInvoker)delegate { UpdateModified(); });
            else
            {
                this.Text = Path.GetFileName(AddonHandle.AddonPath) + (Program.WhitelistOverridden ? "!" : null) +
                    (AddonHandle.CanWrite ? null : " (read-only)") +
                    (AddonHandle.Modified ? "*" : null) + " - SharpGMad";

                tsbSaveAddon.Enabled = AddonHandle.CanWrite && AddonHandle.Modified && (!Program.WhitelistOverridden);
            }
        }

        // Helper functions to populate the folder tree view.
        // This starts the recursion with a string[] of all folders known
        public void GetFolders(string[] Folders)
        {
            TreeNode rootNode = new TreeNode(Path.GetFileName(AddonHandle.AddonPath));
            rootNode.Name = "root";
            rootNode.ImageKey = "gma";
            rootNode.SelectedImageKey = "gma";

            // Generate the list of first-depth (below root level) folders.
            List<string> foldersOnFirstDepth =
                Folders.Select(f => f.Split('/').FirstOrDefault()) // The first folder in the path
                .Distinct().ToList(); // only once.

            RecurseFolders(Folders, rootNode, 1); // We start from the first depth (root level is "0th" depth)

            tvFolders.Nodes.Add(rootNode);
        }

        // This one handles the holy recursion
        public void RecurseFolders(string[] Folders, TreeNode parentNode, int depth)
        {
            // Generate the list of currrent-depth folders.
            List<string> foldersOnCurrentDepth =
                Folders.Select(
                    f => String.Join("/", f.Split('/').Take(depth)) // Select the folders which are on the current level
                ).Distinct().ToList(); // but each folder only once

            foreach (string f in foldersOnCurrentDepth)
            {
                TreeNode node = new TreeNode(f.Split('/').Last());
                node.Name = f;
                node.ImageKey = "folder";
                node.SelectedImageKey = "folder";

                parentNode.Nodes.Add(node);

                // If the folder itself does not contain files (aka: it is not really in the folder list), make it gray
                if (!Folders.Contains<string>(f))
                {
                    node.ForeColor = Color.Gray;
                    node.ImageKey = "emptyFolder";
                    node.SelectedImageKey = "emptyFolder";
                }
            }

            // Iterate those current-level folders
            foreach (string folder in foldersOnCurrentDepth)
            {
                // Get the subfolders from the tree.
                IList<string> fetchedSubfolders = Folders.Where(sf => sf.StartsWith(folder)).ToList();

                // We can't use .Skip(1) in the query above because that would ignore a folder if its parent is an empty one...
                if (fetchedSubfolders.Contains(folder))
                {
                    fetchedSubfolders.Remove(folder); // ... so we remove it here.
                }

                string[] Subfolders = fetchedSubfolders.ToArray();

                // (Now the length should be 0 to prevent recursion if there are no subfolders,
                // but it can also be properly 1 or more if the current folder is empty, but has subfolders.)
                if (Subfolders.Length > 0)
                {
                    RecurseFolders(Subfolders, parentNode.Nodes[folder], depth + 1);
                }
            }
        }

        private void SaveExpandedNodePaths(List<string> pathList, TreeNode node)
        {
            if (!pathList.Contains(node.Name) && node.IsExpanded)
                pathList.Add(node.Name);

            foreach (TreeNode child in node.Nodes)
            {
                if (child.IsExpanded)
                    pathList.Add(child.Name);

                if (child.Nodes.Count > 0)
                    SaveExpandedNodePaths(pathList, child);
            }
        }

        private void ReexpandNodes(List<string> pathsToExpand, TreeNode node)
        {
            if (pathsToExpand.Contains(node.Name) && !node.IsExpanded)
                node.Expand();

            foreach (TreeNode child in node.Nodes)
            {
                if (pathsToExpand.Contains(child.FullPath) && !child.IsExpanded)
                    child.Expand();

                if (child.Nodes.Count > 0)
                    ReexpandNodes(pathsToExpand, child);
            }
        }

        /// <summary>
        /// Updates the folder list (tvFolders) with the state of the addon
        /// </summary>
        private void UpdateFolderList()
        {
            // Reinvocation to fix cross-thread errors.
            if (tvFolders.InvokeRequired)
                this.Invoke((MethodInvoker)delegate { UpdateFolderList(); });
            else
            {
                // Save previous expansion states
                List<string> expandedNodes = new List<string>();
                if (tvFolders.Nodes["root"] != null)
                    SaveExpandedNodePaths(expandedNodes, tvFolders.Nodes["root"]); // Start the recursion

                // Save the previously selected folder as a variable
                string previousNode = String.Empty;
                if (tvFolders.SelectedNode != null)
                    previousNode = tvFolders.SelectedNode.Name;

                // Update the folder list
                tvFolders.Nodes.Clear();

                string[] folderlist =
                    AddonHandle.OpenAddon.Files.GroupBy(f => Path.GetDirectoryName(f.Path).Replace('\\', '/')).Select(f => f.Key)
                    .ToArray();

                GetFolders(folderlist);

                // Reexpand the nodes
                if (tvFolders.Nodes["root"] != null)
                {
                    expandedNodes.Add("root"); // The root node should always be expanded
                    ReexpandNodes(expandedNodes, tvFolders.Nodes["root"]);
                }

                // Reselect the previously selected node
                if (!String.IsNullOrWhiteSpace(previousNode))
                    SelectFolderNode(previousNode);
            }
        }

        // Reselect a folder node based on its full-path.
        private void SelectFolderNode(string fullPath)
        {
            // Reinvocation to fix cross-thread errors.
            if (tvFolders.InvokeRequired)
                this.Invoke((MethodInvoker)delegate { SelectFolderNode(fullPath); });
            else
            {
                // First, state that we will select the root node
                TreeNode nodeToSelect = tvFolders.Nodes["root"];
                IEnumerable<string> pathElements = fullPath.Split('/'); // Get the number of subnodes.

                for (int i = 1; i <= pathElements.Count(); i++)
                {
                    // For each element, select the node below the current one.
                    // (If they exist, of course.)
                    string elementFullPath = String.Join("/", pathElements.Take(i));

                    if (nodeToSelect.Nodes.ContainsKey(elementFullPath)) // We have to make it a fullpath here.
                        nodeToSelect = nodeToSelect.Nodes[elementFullPath];
                }

                tvFolders.SelectedNode = nodeToSelect;
            }
        }

        private void tvFolders_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // When a node is selected, we have to update the file list (lstFiles) to display only the files in the selected node.
            UpdateFileList();
        }

        /// <summary>
        /// Updates the filelist (lstFiles) with the changes administered to the known files.
        /// </summary>
        private void UpdateFileList()
        {
            // Invoke the method if it was called from a thread which is not the thread lstFiles was created in.
            //
            // (For example when fsw_Changed fires.)
            //
            // Prevents the exception:
            // Cross-thread operation not valid: Control 'lstFiles' accessed from a
            // thread other than the thread it was created on.
            if (lstFiles.InvokeRequired)
                this.Invoke((MethodInvoker)delegate { UpdateFileList(); });
            else
            {
                // Reset the export buttons
                tsbPullAll.Enabled = false;
                tsbDropAll.Enabled = false;

                // Clear the list
                lstFiles.Items.Clear();

                // Get and add the files in the current folder
                if (tvFolders.SelectedNode != null)
                {
                    if (tsmiViewShowSubfolders.Checked)
                    {
                        // Add the folders to the list also.
                        // We get the list of subfolders from the child nodes of the currently selected for ease of operation.
                        foreach (TreeNode subfolder in tvFolders.SelectedNode.Nodes)
                        {
                            ListViewItem item = new ListViewItem(subfolder.Text);
                            item.Name = subfolder.Name;
                            item.ImageKey = subfolder.ImageKey;
                            item.Tag = "subfolder";

                            if (item.ImageKey == "emptyFolder")
                                item.ForeColor = Color.Gray;

                            lstFiles.Items.Add(item);
                        }
                    }

                    string nodePath = (tvFolders.SelectedNode.Name == "root" ? "" : tvFolders.SelectedNode.Name);
                    IEnumerable<ContentFile> filesInFolder = AddonHandle.OpenAddon.Files
                        .Where(f => Path.GetDirectoryName(f.Path).Replace('\\', '/') == nodePath);

                    foreach (ContentFile cfile in filesInFolder)
                    {
                        ListViewItem item = new ListViewItem(Path.GetFileName(cfile.Path));
                        item.Name = cfile.Path; // Store the full path as an internal value for easier use
                        item.ImageKey = "file";

                        // Add subitems for Details view
                        ListViewItem.ListViewSubItem type = new ListViewItem.ListViewSubItem();

                        // Get the extension name from the known list
                        string typestring;
                        if (Whitelist.FileTypes.ContainsKey(Path.GetExtension(cfile.Path).TrimStart('.').ToLowerInvariant()))
                            typestring = Whitelist.FileTypes[Path.GetExtension(cfile.Path).TrimStart('.').ToLowerInvariant()];
                        else
                            typestring = Path.GetExtension(cfile.Path).TrimStart('.').ToUpperInvariant() + " file";

                        type.Text = typestring;

                        ListViewItem.ListViewSubItem size = new ListViewItem.ListViewSubItem();
                        size.Text = ((int)cfile.Size).HumanReadableSize();

                        item.SubItems.Add(type);
                        item.SubItems.Add(size);

                        IEnumerable<FileWatch> watch = AddonHandle.WatchedFiles.Where(f => f.ContentPath == cfile.Path);
                        if (watch.Count() == 1)
                        {
                            tsbDropAll.Enabled = true; // At least one file is exported
                            item.ForeColor = Color.Blue;

                            if (watch.First().Modified)
                            {
                                tsbPullAll.Enabled = AddonHandle.CanWrite; // At least one file is modified externally
                                item.ForeColor = Color.Indigo;
                            }
                        }

                        lstFiles.Items.Add(item);
                    }
                }
            }
        }

        /// <summary>
        /// Update the status label on the bottom of the form.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <param name="foreColor">The color the text should have.</param>
        private void UpdateStatus(string text, Color foreColor = default(Color))
        {
            if (foreColor == default(Color))
                foreColor = System.Drawing.SystemColors.ControlText;

            tsslStatus.ForeColor = foreColor;
            tsslStatus.Text = (Program.WhitelistOverridden ? "!Cannot modify addons because the whitelist had been overridden! " : null) +
                "[" + DateTime.Now.ToString() + "] " + text;
        }

        /// <summary>
        /// Updates the metadata panel with the information fetched from the current addon.
        /// </summary>
        private void UpdateMetadataPanel()
        {
            txtMetadataTitle.Text = AddonHandle.OpenAddon.Title;
            /*txtMetadataAuthor.Text = AddonHandle.OpenAddon.Author;*/
            txtMetadataDescription.Text = AddonHandle.OpenAddon.Description;

            cmbMetadataType.Items.Clear();
            cmbMetadataType.Items.AddRange(Tags.Type);
            cmbMetadataType.SelectedItem = AddonHandle.OpenAddon.Type;

            cmbMetadataTag1.Items.Clear();
            cmbMetadataTag1.Items.AddRange(Tags.Misc);
            cmbMetadataTag1.Items.Add("");
            try
            {
                cmbMetadataTag1.SelectedItem = AddonHandle.OpenAddon.Tags[0];
            }
            catch (ArgumentOutOfRangeException)
            {
                // No first tag, select the empty one
                cmbMetadataTag1.SelectedItem = "";
            }

            cmbMetadataTag2.Items.Clear();
            cmbMetadataTag2.Items.AddRange(Tags.Misc);
            cmbMetadataTag2.Items.Add("");
            try
            {
                cmbMetadataTag2.SelectedItem = AddonHandle.OpenAddon.Tags[1];
            }
            catch (ArgumentOutOfRangeException)
            {
                // No second tag, select the empty one
                cmbMetadataTag2.SelectedItem = "";
            }
        }

        /// <summary>
        /// Closes the currently open addon connection.
        /// </summary>
        private void UnloadAddon()
        {
            txtMetadataTitle.Text = String.Empty;
            /*txtMetadataAuthor.Text = String.Empty;*/
            cmbMetadataType.Items.Clear();
            cmbMetadataTag1.Items.Clear();
            cmbMetadataTag2.Items.Clear();
            txtMetadataDescription.Text = String.Empty;

            lstFiles.Items.Clear();
            lstFiles.Groups.Clear();

            this.Text = "SharpGMad" + (Program.WhitelistOverridden ? "!" : null);
            tsbSaveAddon.Enabled = false;

            if (AddonHandle != null)
                AddonHandle.Close();
            AddonHandle = null;

            tsbUpdateMetadata.Enabled = false;
            tsbDiscardMetadataChanges_Click(null, new EventArgs());
            tsbAddFile.Enabled = false;
        }

        // Dock the txtDescription text box.
        // It gets automatically resized when the form is resized.
        Size txtDescriptionSizeDifference;
        private void Main_Load(object sender, EventArgs e)
        {
            txtDescriptionSizeDifference = new Size(pnlRightSide.Size.Width - txtMetadataDescription.Size.Width,
                pnlRightSide.Size.Height - txtMetadataDescription.Size.Height);

            UpdateStatus("SharpGMad welcomes you!");
        }

        private void Main_Resize(object sender, EventArgs e)
        {
            txtMetadataDescription.Size = new Size(pnlRightSide.Size.Width - txtDescriptionSizeDifference.Width,
                pnlRightSide.Size.Height - txtDescriptionSizeDifference.Height);
        }
        // Dock the txtDescription text box.

        private void tsmiLegacyCreate_Click(object sender, EventArgs e)
        {
            LegacyCreate lcForm = new LegacyCreate();
            lcForm.Show(this);
        }

        private void tsmiLegacyExtract_Click(object sender, EventArgs e)
        {
            LegacyExtract leForm = new LegacyExtract();
            leForm.Show(this);
        }

        private void tsbAddFile_Click(object sender, EventArgs e)
        {
            if (AddonHandle == null || Program.WhitelistOverridden)
                return;

            // If there is no value for file filtering, load a file list
            if (ofdAddFile.Filter == String.Empty)
            {
                string filter = String.Empty;
                foreach (KeyValuePair<string, string[]> filetype in Whitelist.WildcardFileTypes)
                {
                    filter += filetype.Key + "|" + String.Join(";", filetype.Value) + "|";
                }
                filter += "All files|*.*";

                ofdAddFile.Filter = filter;
                ofdAddFile.FilterIndex = Whitelist.WildcardFileTypes.Count + 1;
            }

            DialogResult result = ofdAddFile.ShowDialog();
            if (result != DialogResult.Cancel)
            {
                string filename = ofdAddFile.FileName.Replace("\\", "/");

                try
                {
                    AddonHandle.OpenAddon.CheckRestrictions(Whitelist.GetMatchingString(filename));
                    AddonHandle.AddFile(Whitelist.GetMatchingString(filename), File.ReadAllBytes(ofdAddFile.FileName));
                }
                catch (IOException)
                {
                    MessageBox.Show("Error happened while reading " + ofdAddFile.FileName, "Add file",
                        MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    return;
                }
                catch (IgnoredException)
                {
                    MessageBox.Show("File is ignored (" + ofdAddFile.FileName + ")", "Add file",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                catch (WhitelistException)
                {
                    MessageBox.Show("File is not whitelisted (" + ofdAddFile.FileName + ")", "Add file",
                        MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    return;
                }
                catch (ArgumentException)
                {
                    MessageBox.Show("Similar file has already been added (" + ofdAddFile.FileName + ")",
                        "Add file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                UpdateModified();
                UpdateFolderList();
                UpdateFileList();
                ofdAddFile.Reset();
            }
        }

        private void lstFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListView lv = (ListView)sender;

            if (lv.SelectedItems.Count == 1)
            {
                // One file is selected
                if (lv.FocusedItem != null)
                {
                    if ((string)lv.FocusedItem.Tag != "subfolder")
                    {
                        // Allow remove, extract and execution
                        tsmFileRemove.Visible = true;
                        tsmFileRemove.Enabled = AddonHandle.CanWrite && !Program.WhitelistOverridden;

                        tsmFileExtract.Enabled = true;
                        tsmFileExtract.Visible = true;

                        tsmFileShellExec.Enabled = true;
                        tsmFileShellExec.Visible = true;

                        // Allow export (and related) options
                        IEnumerable<FileWatch> isExported = AddonHandle.WatchedFiles.Where(f => f.ContentPath ==
                            lstFiles.FocusedItem.Name);
                        if (isExported.Count() == 0)
                        {
                            // Export is the file is not exported
                            tsmFileExportTo.Enabled = AddonHandle.CanWrite && !Program.WhitelistOverridden;
                            tsmFilePull.Enabled = false;
                            tsmFileDropExport.Enabled = false;
                        }
                        else
                        {
                            // Pull (applicable if the file is changed) and drop
                            tsmFileExportTo.Enabled = false;
                            tsmFilePull.Enabled = isExported.First().Modified && AddonHandle.CanWrite && !Program.WhitelistOverridden;
                            tsmFileDropExport.Enabled = true;
                        }

                        // But the buttons should be visible
                        tssExportSeparator.Visible = true;
                        tsmFileExportTo.Visible = true;
                        tsmFilePull.Visible = true;
                        tsmFileDropExport.Visible = true;
                    }
                    else
                    {
                        // Subfolder selected

                        // Allow remove and extract
                        tsmFileRemove.Visible = true;
                        tsmFileRemove.Enabled = AddonHandle.CanWrite && !Program.WhitelistOverridden;

                        tsmFileExtract.Enabled = true;
                        tsmFileExtract.Visible = true;

                        // Disallow (and hide) everything else
                        tsmFileShellExec.Enabled = false;
                        tsmFileShellExec.Visible = false;

                        tssExportSeparator.Visible = false;

                        tsmFileExportTo.Enabled = false;
                        tsmFileExportTo.Visible = false;

                        tsmFilePull.Enabled = false;
                        tsmFilePull.Visible = false;

                        tsmFileDropExport.Enabled = false;
                        tsmFileDropExport.Visible = false;
                    }
                }
            }
            else if (lv.SelectedItems.Count > 1)
            {
                // Multiple entries only support removal and extraction
                tsmFileRemove.Enabled = !Program.WhitelistOverridden;
                tsmFileRemove.Visible = true;

                tsmFileExtract.Enabled = true;
                tsmFileExtract.Visible = true;

                tsmFileShellExec.Enabled = false;
                tsmFileShellExec.Visible = false;

                tssExportSeparator.Visible = false;

                tsmFileExportTo.Enabled = false;
                tsmFileExportTo.Visible = false;

                tsmFilePull.Enabled = false;
                tsmFilePull.Visible = false;

                tsmFileDropExport.Enabled = false;
                tsmFileDropExport.Visible = false;
            }
            else
            {
                // Nothing if there are no entries selected
                tsmFileRemove.Enabled = false;
                tsmFileRemove.Visible = false;

                tsmFileExtract.Enabled = false;
                tsmFileExtract.Visible = false;

                tsmFileShellExec.Enabled = false;
                tsmFileShellExec.Visible = false;

                tssExportSeparator.Visible = false;

                tsmFileExportTo.Enabled = false;
                tsmFileExportTo.Visible = false;

                tsmFilePull.Enabled = false;
                tsmFilePull.Visible = false;

                tsmFileDropExport.Enabled = false;
                tsmFileDropExport.Visible = false;
            }
        }

        private void lstFiles_MouseClick(object sender, MouseEventArgs e)
        {
            ListView lv = (ListView)sender;

            if (e.Button == MouseButtons.Right)
                if (lv.FocusedItem != null)
                    if (lv.FocusedItem.Bounds.Contains(e.Location) == true)
                        cmsFileEntry.Show(Cursor.Position);
        }

        private void lstFiles_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListView lv = (ListView)sender;

            if (e.Button == MouseButtons.Left && e.Clicks == 2)
                if (lv.FocusedItem.Bounds.Contains(e.Location) == true)
                    if ((string)lv.FocusedItem.Tag != "subfolder")
                        tsmFileShellExec_Click(sender, e);
                    else
                        SelectFolderNode(lv.FocusedItem.Name);
        }

        private void lstFiles_KeyDown(object sender, KeyEventArgs e)
        {
            ListView lv = (ListView)sender;

            if (e.KeyCode == Keys.Delete)
            {
                if (lv.FocusedItem != null)
                    //if ((string)lv.FocusedItem.Tag != "subfolder")
                        tsmFileRemove_Click(sender, e);
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
            {
                if (lv.FocusedItem != null)
                    if ((string)lv.FocusedItem.Tag != "subfolder")
                        tsmFileShellExec_Click(sender, e);
                    else
                        SelectFolderNode(lv.FocusedItem.Name);
            }
        }

        private void tsmFileRemove_Click(object sender, EventArgs e)
        {
            if (!AddonHandle.CanWrite || Program.WhitelistOverridden)
                return;

            if (lstFiles.SelectedItems.Count == 1)
            {
                if (lstFiles.FocusedItem != null)
                {
                    if ((string)lstFiles.FocusedItem.Tag != "subfolder")
                    {
                        DialogResult remove = MessageBox.Show("Really remove " +
                            lstFiles.FocusedItem.Name + "?", "Remove file",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (remove == DialogResult.Yes)
                        {
                            try
                            {
                                AddonHandle.RemoveFile(lstFiles.FocusedItem.Name);
                            }
                            catch (FileNotFoundException)
                            {
                                MessageBox.Show("File not in archive (" + lstFiles.FocusedItem.Name + ")", "Remove file",
                                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                return;
                            }

                            UpdateStatus("Removed file " + lstFiles.FocusedItem.Name);
                            UpdateModified();
                            UpdateFolderList();
                            UpdateFileList();
                        }
                    }
                    else
                    {
                        // Removal of a subfolder
                        List<string> entriesInFolder = AddonHandle.OpenAddon.Files
                            .Where(f => f.Path.StartsWith(lstFiles.FocusedItem.Name)).Select(f => f.Path).ToList();

                        DialogResult remove = MessageBox.Show("Really remove " +
                            lstFiles.FocusedItem.Name + "/ containing " + entriesInFolder.Count + " file" +
                            (entriesInFolder.Count >= 2 ? "s" : null) + "?", "Remove folder",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (remove == DialogResult.Yes)
                        {
                            int failedToRemove = 0;

                            foreach (string entry in entriesInFolder)
                            {
                                // Now it is, yet again the conventional file removal.
                                try
                                {
                                    AddonHandle.RemoveFile(entry);
                                }
                                catch (FileNotFoundException)
                                {
                                    // This should not happen as the expression loaded _found_ files, but yeah...
                                    failedToRemove++;
                                }
                            }

                            UpdateStatus("Removed " + (entriesInFolder.Count - failedToRemove) + " files" +
                                (failedToRemove >= 1 ? " (" + Convert.ToString(failedToRemove) + " failed)" : null));
                            UpdateModified();
                            UpdateFolderList();
                            UpdateFileList();
                        }
                    }
                }
            }
            else if (lstFiles.SelectedItems.Count >= 1)
            {
                // We need to separate the selected subfolders and files
                List<string> subfolders = new List<string>();
                List<string> files = new List<string>();

                foreach (ListViewItem item in lstFiles.SelectedItems)
                    if ((string)item.Tag == "subfolder")
                        subfolders.Add(item.Name);
                    else
                        files.Add(item.Name);
                
                // Put together a nice prompt :)
                string countString = String.Empty;
                string verbString = String.Empty;
                
                if (subfolders.Count >= 1)
                {
                    countString += subfolders.Count + " folder";
                    if (subfolders.Count >= 2)
                        countString += "s";

                    verbString += "folders";
                }
                
                if (subfolders.Count >= 1 && files.Count >= 1)
                {
                    countString += " and ";
                    verbString += " and ";
                }

                if (files.Count >= 1)
                {
                    countString += files.Count + " file";
                    if (files.Count >= 2)
                        countString += "s";

                    verbString += "files";
                }


                DialogResult remove = MessageBox.Show("Remove " + countString + "?", "Remove " + verbString + "?",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (remove == DialogResult.Yes)
                {
                    List<string> failed_paths = new List<string>(lstFiles.SelectedItems.Count);

                    // Removing files is conventional
                    foreach (string file in files)
                    {
                        try
                        {
                            AddonHandle.RemoveFile(file);
                        }
                        catch (FileNotFoundException)
                        {
                            failed_paths.Add(file);
                        }
                    }

                    // Removing folders, however, is not.
                    foreach (string folder in subfolders)
                    {
                        // First, build a list of files in the current folder
                        List<string> entriesInFolder = AddonHandle.OpenAddon.Files
                            .Where(f => f.Path.StartsWith(folder)).Select(f => f.Path).ToList();

                        foreach (string entry in entriesInFolder)
                        {
                            // Now it is, yet again the conventional file removal.
                            try
                            {
                                AddonHandle.RemoveFile(entry);
                            }
                            catch (FileNotFoundException)
                            {
                                // This should not happen as the expression loaded _found_ files, but yeah...
                                failed_paths.Add(entry);
                            }
                        }
                    }
                    
                    if (failed_paths.Count == 0)
                        UpdateStatus("Removed " + countString);
                    else if (failed_paths.Count == 1)
                        MessageBox.Show("Failed to remove " + failed_paths[0], "Remove " + verbString,
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    else if (failed_paths.Count > 1)
                    {
                        DialogResult showFailedFiles = MessageBox.Show(Convert.ToString(failed_paths.Count) +
                            " " + verbString + " failed to remove.\n\nShow a list of failures?", "Remove " + verbString,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                        if (showFailedFiles == DialogResult.Yes)
                        {
                            string temppath = ContentFile.GenerateExternalPath(
                                    new Random().Next() + "_failedRemovals") + ".txt";

                            try
                            {
                                File.WriteAllText(temppath,
                                    "These files failed to get removed:\r\n\r\n" +
                                    String.Join("\r\n", failed_paths.ToArray()));
                            }
                            catch (Exception)
                            {
                                MessageBox.Show("Can't show the list, an error happened generating it.", "Remove " + verbString,
                                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                return;
                            }

                            // The file will be opened by the user's default text file handler (Notepad?)
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(temppath)
                            {
                                UseShellExecute = true,
                            });
                        }
                    }

                    UpdateModified();
                    UpdateFolderList();
                    UpdateFileList();
                }
            }
        }

        private void tsbUpdateMetadata_Click(object sender, EventArgs e)
        {
            if (!AddonHandle.CanWrite || Program.WhitelistOverridden)
                return;

            // Use a toggle mechanism to enable and disable the changing of metadata
            if (!tsbUpdateMetadata.Checked)
            {
                txtMetadataTitle.ReadOnly = false;
                //txtMetadataAuthor.ReadOnly = false;
                cmbMetadataType.Enabled = true;
                cmbMetadataTag1.Enabled = true;
                cmbMetadataTag2.Enabled = true;
                txtMetadataDescription.ReadOnly = false;

                tsbUpdateMetadata.Checked = true;
                tsbUpdateMetadata.Text = "Save changes";
                tsbUpdateMetadata.ToolTipText = "Save the metadata changes";
                tsbDiscardMetadataChanges.Enabled = true;
                tsbDiscardMetadataChanges.Visible = true;
            }
            else if (tsbUpdateMetadata.Checked)
            {
                // Save the metadata changes
                if (cmbMetadataTag1.SelectedItem != null && cmbMetadataTag2.SelectedItem != null)
                {
                    if (cmbMetadataTag1.SelectedItem.ToString() == cmbMetadataTag2.SelectedItem.ToString() &&
                        !(cmbMetadataTag1.SelectedItem.ToString() == "" || cmbMetadataTag2.SelectedItem.ToString() == ""))
                    {
                        MessageBox.Show("The same tag is selected twice", "Update metadata",
                            MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                        return;
                    }
                }

                if (cmbMetadataType.SelectedItem == null || !Tags.TypeExists(cmbMetadataType.SelectedItem.ToString()))
                {
                    MessageBox.Show("Invalid type selected", "Update metadata",
                        MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return;
                }

                if (cmbMetadataTag1.SelectedItem != null)
                {
                    if (!Tags.TagExists(cmbMetadataTag1.SelectedItem.ToString()) && cmbMetadataTag1.SelectedItem.ToString() != "")
                    {
                        MessageBox.Show("Invalid tag selected", "Update metadata",
                            MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                        return;
                    }
                }

                if (cmbMetadataTag2.SelectedItem != null)
                {
                    if (!Tags.TagExists(cmbMetadataTag2.SelectedItem.ToString()) && cmbMetadataTag2.SelectedItem.ToString() != "")
                    {
                        MessageBox.Show("Invalid tag selected", "Update metadata",
                            MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                        return;
                    }
                }

                AddonHandle.OpenAddon.Title = txtMetadataTitle.Text;
                //AddonHandle.OpenAddon.Author = txtMetadataAuthor.Text;
                if (cmbMetadataType.SelectedItem != null)
                    AddonHandle.OpenAddon.Type = cmbMetadataType.SelectedItem.ToString();
                AddonHandle.OpenAddon.Tags = new List<string>(2);
                if (cmbMetadataTag1.SelectedItem != null && cmbMetadataTag1.SelectedItem.ToString() != "")
                    AddonHandle.OpenAddon.Tags.Add(cmbMetadataTag1.SelectedItem.ToString());
                if (cmbMetadataTag2.SelectedItem != null && cmbMetadataTag2.SelectedItem.ToString() != "")
                    AddonHandle.OpenAddon.Tags.Add(cmbMetadataTag2.SelectedItem.ToString());
                AddonHandle.OpenAddon.Description = txtMetadataDescription.Text;

                AddonHandle.Modified = true;
                UpdateModified();
                UpdateMetadataPanel(); // Force reload the values in the metadata panel so we're sure the addon is correctly set.

                // Resets the controls
                txtMetadataTitle.ReadOnly = true;
                //txtMetadataAuthor.ReadOnly = true;
                cmbMetadataType.Enabled = false;
                cmbMetadataTag1.Enabled = false;
                cmbMetadataTag2.Enabled = false;
                txtMetadataDescription.ReadOnly = true;

                tsbUpdateMetadata.Checked = false;
                tsbUpdateMetadata.Text = "Update metadata";
                tsbUpdateMetadata.ToolTipText = "Change the metadata of the addon";
                tsbDiscardMetadataChanges.Enabled = false;
                tsbDiscardMetadataChanges.Visible = false;
            }
        }

        private void tsbDiscardMetadataChanges_Click(object sender, EventArgs e)
        {
            if (AddonHandle is RealtimeAddon)
            {
                // Reset the metadata information to what is already in memory and lock the controls
                UpdateMetadataPanel();

                txtMetadataTitle.ReadOnly = true;
                //txtMetadataAuthor.ReadOnly = true;
                cmbMetadataType.Enabled = false;
                cmbMetadataTag1.Enabled = false;
                cmbMetadataTag2.Enabled = false;
                txtMetadataDescription.ReadOnly = true;

                tsbUpdateMetadata.Checked = false;
                tsbDiscardMetadataChanges.Enabled = false;
                tsbDiscardMetadataChanges.Visible = false;
            }
        }

        private void tsbSaveAddon_Click(object sender, EventArgs e)
        {
            if (!AddonHandle.CanWrite || Program.WhitelistOverridden)
                return;

            if (!tsbUpdateMetadata.Checked)
            {
                if (AddonHandle.Modified)
                {
                    try
                    {
                        AddonHandle.Save();
                    }
                    catch (AddonJSONException ex)
                    // Writer.Create access addon.DescriptionJSON which calls
                    // Json.BuildDescription() which can throw the AddonJSONException
                    {
                        MessageBox.Show("Error happened: " + ex.Message + "\n\n" +
                            "This is usually caused by invalid metadata values.", "Save addon",
                             MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        return;
                    }

                    if (!(e is FormClosingEventArgs))
                        UpdateStatus("Addon saved successfully");

                    UpdateModified();
                }
            }
            else if (tsbUpdateMetadata.Checked == true)
            {
                MessageBox.Show("There might be unsaved changes to the metadata.", "Save addon",
                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (AddonHandle is RealtimeAddon && AddonHandle.Modified && !Program.WhitelistOverridden)
            {
                DialogResult yesClose = MessageBox.Show("Save the addon before quitting?",
                    Path.GetFileName(AddonHandle.AddonPath), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                switch (yesClose)
                {
                    case DialogResult.Cancel:
                        e.Cancel = true;
                        break;
                    case DialogResult.No:
                        // Noop, we just close.
                        break;
                    case DialogResult.Yes:
                        tsbSaveAddon_Click(sender, e); // Invoke the addon saving mechanism
                        break;
                    default:
                        break;
                }
            }

            if (AddonHandle is RealtimeAddon)
            {
                UnloadAddon();
            }
        }

        private void tsbCreateAddon_Click(object sender, EventArgs e)
        {
            if (Program.WhitelistOverridden)
            {
                tsbCreateAddon.Enabled = !Program.WhitelistOverridden;
                return;
            }

            DialogResult dropChanges = new DialogResult();
            if (AddonHandle is RealtimeAddon && AddonHandle.Modified && !Program.WhitelistOverridden)
            {
                dropChanges = MessageBox.Show("Open an another addon without saving the current one?\nYou'll lose the changes.",
                    "Addon already open", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            }

            if (dropChanges == DialogResult.Yes || AddonHandle == null ||
                (AddonHandle is RealtimeAddon && !AddonHandle.Modified))
            {
                UnloadAddon();
                DialogResult file = sfdAddon.ShowDialog();

                if (file == DialogResult.OK)
                {
                    try
                    {
                        AddonHandle = RealtimeAddon.New(sfdAddon.FileName);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("There was a problem creating the addon.", "Create new addon",
                            MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        return;
                    }

                    AddonHandle.OpenAddon.Title = Path.GetFileNameWithoutExtension(sfdAddon.FileName);
                    /*AddonHandle.OpenAddon.Author = "Author Name"; // This is currently not changable*/
                    AddonHandle.OpenAddon.Description = String.Empty;
                    AddonHandle.OpenAddon.Type = String.Empty;
                    AddonHandle.OpenAddon.Tags = new List<string>();
                    tsbUpdateMetadata_Click(sender, e); // This will make the metadata change enabled to set the initial values

                    // But the "Discard" button must be disabled so that the user cannot leave the metadata blank
                    tsbDiscardMetadataChanges.Enabled = false;
                    tsbDiscardMetadataChanges.Visible = false;

                    UpdateModified();
                    UpdateMetadataPanel();
                    UpdateFolderList();
                    UpdateFileList();

                    tsbAddFile.Enabled = true;
                    tsbUpdateMetadata.Enabled = true;
                }
            }
        }

        private void tsmFileExportTo_Click(object sender, EventArgs e)
        {
            if (!AddonHandle.CanWrite || Program.WhitelistOverridden)
                return;

            if (lstFiles.FocusedItem != null)
            {
                if ((string)lstFiles.FocusedItem.Tag != "subfolder")
                {
                    string contentPath = lstFiles.FocusedItem.Name;

                    sfdExportFile.FileName = Path.GetFileName(contentPath);
                    sfdExportFile.DefaultExt = Path.GetExtension(contentPath);
                    sfdExportFile.Title = "Export " + Path.GetFileName(contentPath) + " to...";

                    DialogResult save = sfdExportFile.ShowDialog();

                    if (save == DialogResult.OK)
                    {
                        string exportPath = sfdExportFile.FileName;

                        // OK happens also if the user said overwrite. Delete the file if so.
                        if (File.Exists(exportPath))
                            File.Delete(exportPath);

                        try
                        {
                            AddonHandle.ExportFile(contentPath, exportPath);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            MessageBox.Show("Another file is already exported at " + exportPath, "Export file",
                                MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            return;
                        }
                        catch (ArgumentException)
                        {
                            MessageBox.Show("The file is already exported elsewhere.", "Export file",
                                MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            return;
                        }
                        catch (IOException)
                        {
                            MessageBox.Show("The file cannot be created on the disk.", "Export file",
                                MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            return;
                        }

                        // Add a custom event handler so that the form gets updated when a file is pullable.
                        AddonHandle.WatchedFiles.Where(f => f.ContentPath == contentPath).First().FileChanged +=
                            fsw_Changed;
                    }

                    sfdExportFile.Reset();
                    UpdateFolderList();
                    UpdateFileList();
                }
            }
        }

        private void fsw_Changed(object sender, FileSystemEventArgs e)
        {
            UpdateModified();
            UpdateFolderList();
            UpdateFileList();
            this.Invoke((MethodInvoker)delegate { UpdateStatus("The file " + e.Name + " changed externally.", Color.Purple); });
        }

        private void tsmFileDropExport_Click(object sender, EventArgs e)
        {
            if (lstFiles.FocusedItem != null)
                if ((string)lstFiles.FocusedItem.Tag != "subfolder")
                    DropFileExport(lstFiles.FocusedItem.Name);
        }

        private void tsbDropAll_Click(object sender, EventArgs e)
        {
            List<string> pathsFailedToDelete = new List<string>();
            List<FileWatch> watchedFiles = new List<FileWatch>(AddonHandle.WatchedFiles);

            foreach (FileWatch watch in watchedFiles)
            {
                try
                {
                    AddonHandle.DropExport(watch.ContentPath);
                }
                catch (IOException)
                {
                    pathsFailedToDelete.Add(watch.FilePath);
                }
            }

            watchedFiles.Clear();

            if (pathsFailedToDelete.Count == 0)
                UpdateStatus("Removed all current exported files");
            else if (pathsFailedToDelete.Count == 1)
                MessageBox.Show("Failed to remove " + pathsFailedToDelete[0], "Drop all exports",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (pathsFailedToDelete.Count > 1)
            {
                DialogResult showFailedFiles = MessageBox.Show(pathsFailedToDelete.Count + " files failed to get removed." +
                    "\n\nShow a list of failures?", "Drop all exports", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (showFailedFiles == DialogResult.Yes)
                {
                    string temppath = ContentFile.GenerateExternalPath(
                            new Random().Next() + "_failedRemovals") + ".txt";

                    try
                    {
                        File.WriteAllText(temppath,
                            "These files failed to get removed:\r\n\r\n" +
                            String.Join("\r\n", pathsFailedToDelete.ToArray()));
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Can't show the list, an error happened generating it.", "Drop all exports",
                            MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        return;
                    }

                    // The file will be opened by the user's default text file handler (Notepad?)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(temppath)
                    {
                        UseShellExecute = true,
                    });
                }
            }

            UpdateFolderList();
            UpdateFileList();
        }

        private void DropFileExport(string filename)
        {
            try
            {
                AddonHandle.DropExport(filename);
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("This file is not exported!", "Drop extract",
                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            catch (IOException ex)
            {
                MessageBox.Show("Failed to delete the exported file:" +
                    "\n" + AddonHandle.WatchedFiles.Where(f => f.ContentPath == filename).First().FilePath +
                    ".\n\nBecause error happened: " + ex.Message, "Drop extract",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            UpdateFolderList();
            UpdateFileList();
        }

        private void tsmFilePull_Click(object sender, EventArgs e)
        {
            if (lstFiles.FocusedItem != null)
                if ((string)lstFiles.FocusedItem.Tag != "subfolder")
                    PullFile(lstFiles.FocusedItem.Name);
        }

        private void tsbPullAll_Click(object sender, EventArgs e)
        {
            List<string> pathsFailedToPull = new List<string>();

            foreach (FileWatch watch in AddonHandle.WatchedFiles)
            {
                if (watch.Modified)
                {
                    try
                    {
                        AddonHandle.Pull(watch.ContentPath);
                    }
                    catch (IOException)
                    {
                        pathsFailedToPull.Add(watch.ContentPath + " from " + watch.FilePath);
                        continue;
                    }
                }
            }

            if (pathsFailedToPull.Count == 0)
                UpdateStatus("Successfully pulled all changes");
            else if (pathsFailedToPull.Count == 1)
                MessageBox.Show("Failed to pull " + pathsFailedToPull[0], "Pull all changes",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (pathsFailedToPull.Count > 1)
            {
                DialogResult showFailedFiles = MessageBox.Show(pathsFailedToPull.Count + " files failed to get pulled." +
                    "\n\nShow a list of failures?", "Pull all changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (showFailedFiles == DialogResult.Yes)
                {
                    string temppath = ContentFile.GenerateExternalPath(
                            new Random().Next() + "_failedPulls") + ".txt";

                    try
                    {
                        File.WriteAllText(temppath,
                            "These files failed to get pulled:\r\n\r\n" +
                            String.Join("\r\n", pathsFailedToPull.ToArray()));
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Can't show the list, an error happened generating it.", "Pull all changes",
                            MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        return;
                    }

                    // The file will be opened by the user's default text file handler (Notepad?)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(temppath)
                    {
                        UseShellExecute = true,
                    });
                }
            }

            UpdateModified();
            UpdateFolderList();
            UpdateFileList();
        }

        /// <summary>
        /// Pulls the changes of the specified file from its exported version.
        /// </summary>
        /// <param name="filename">The internal path of the file changes should be pulled into.
        /// The exported path is known automatically.</param>
        private void PullFile(string filename)
        {
            if (!AddonHandle.CanWrite || Program.WhitelistOverridden)
                return;

            try
            {
                AddonHandle.Pull(filename);
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("This file is not exported!", "Pull changes",
                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            catch (IOException ex)
            {
                MessageBox.Show("Failed to open the exported file (" +
                    AddonHandle.WatchedFiles.Where(f => f.ContentPath == filename).First().FilePath +
                    ").\n\nAn error happened: " + ex.Message, "Pull changes",
                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            UpdateStatus("Successfully pulled changes for " + filename);
            UpdateModified();
            UpdateFolderList();
            UpdateFileList();
        }

        private void tsmFileExtract_Click(object sender, EventArgs e)
        {
            if (lstFiles.SelectedItems.Count == 1)
            {
                if (lstFiles.FocusedItem != null)
                {
                    if ((string)lstFiles.FocusedItem.Tag != "subfolder")
                    {
                        string contentPath = lstFiles.FocusedItem.Name;

                        sfdExportFile.FileName = Path.GetFileName(contentPath);
                        sfdExportFile.DefaultExt = Path.GetExtension(contentPath);
                        sfdExportFile.Title = "Extract " + Path.GetFileName(contentPath) + " to...";

                        DialogResult save = sfdExportFile.ShowDialog();

                        if (save == DialogResult.OK)
                        {
                            string extractPath = sfdExportFile.FileName;

                            try
                            {
                                AddonHandle.ExtractFile(contentPath, extractPath);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                MessageBox.Show("This file is already extracted at " + extractPath, "Extract file",
                                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                return;
                            }
                            catch (IOException)
                            {
                                MessageBox.Show("There was a problem creating the file.", "Extract file",
                                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                return;
                            }
                        }

                        sfdExportFile.Reset();
                    }
                    else
                    {
                        // Extracting a subfolder.
                        List<string> files = AddonHandle.OpenAddon.Files
                            .Where(f => f.Path.StartsWith(lstFiles.FocusedItem.Name)).Select(f => f.Path).ToList();

                        if (files.Count > 0)
                        {
                            fbdFileExtractMulti.Description = "Extract " + lstFiles.FocusedItem.Name + "/ " +
                                "containing " + files.Count + " file" +
                                (files.Count >= 2 ? "s" : null) + " to...";
                            fbdFileExtractMulti.SelectedPath = Directory.GetCurrentDirectory();

                            DialogResult save = fbdFileExtractMulti.ShowDialog();
                            if (save == DialogResult.OK)
                            {
                                string extractPath = fbdFileExtractMulti.SelectedPath;

                                List<string> failed_paths = new List<string>(files.Count);

                                foreach (string file in files)
                                {
                                    string outpath = extractPath + Path.DirectorySeparatorChar + file.Substring(tvFolders.SelectedNode.Name.Length);

                                    try
                                    {
                                        // We might need to create the directory tree for the extract
                                        if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                                            Directory.CreateDirectory(Path.GetDirectoryName(outpath));

                                        AddonHandle.ExtractFile(file, outpath);
                                    }
                                    catch (Exception)
                                    {
                                        failed_paths.Add(file);
                                        continue;
                                    }
                                }

                                if (failed_paths.Count == 0)
                                    UpdateStatus("Extracted " + files.Count() + " files from " + lstFiles.FocusedItem.Name + "/ successfully");
                                else if (failed_paths.Count == 1)
                                    MessageBox.Show("Failed to extract " + failed_paths[0], "Extract folder",
                                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                else if (failed_paths.Count > 1)
                                {
                                    DialogResult showFailedFiles = MessageBox.Show(Convert.ToString(failed_paths.Count) +
                                        " files failed to extract.\n\nShow a list of failures?", "Extract folder",
                                        MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                                    if (showFailedFiles == DialogResult.Yes)
                                    {
                                        string temppath = ContentFile.GenerateExternalPath(
                                                new Random().Next() + "_failedExtracts") + ".txt";

                                        try
                                        {
                                            File.WriteAllText(temppath,
                                                "These files failed to extract:\r\n\r\n" +
                                                String.Join("\r\n", failed_paths.ToArray()));
                                        }
                                        catch (Exception)
                                        {
                                            MessageBox.Show("Can't show the list, an error happened generating it.", "Extract folder",
                                                MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                            return;
                                        }

                                        // The file will be opened by the user's default text file handler (Notepad?)
                                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(temppath)
                                        {
                                            UseShellExecute = true,
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (lstFiles.SelectedItems.Count >= 1)
            {
                // We need to separate the selected subfolders and files
                List<string> subfolders = new List<string>();
                List<string> files = new List<string>();

                foreach (ListViewItem item in lstFiles.SelectedItems)
                    if ((string)item.Tag == "subfolder")
                        subfolders.Add(item.Name);
                    else
                        files.Add(item.Name);

                // Put together a nice prompt :)
                string countString = String.Empty;
                string verbString = String.Empty;

                if (subfolders.Count >= 1)
                {
                    countString += subfolders.Count + " folder";
                    if (subfolders.Count >= 2)
                        countString += "s";

                    verbString += "folders";
                }

                if (subfolders.Count >= 1 && files.Count >= 1)
                {
                    countString += " and ";
                    verbString += " and ";
                }

                if (files.Count >= 1)
                {
                    countString += files.Count + " file";
                    if (files.Count >= 2)
                        countString += "s";

                    verbString += "files";
                }

                fbdFileExtractMulti.Description = "Extract the selected " + countString + " to...";
                fbdFileExtractMulti.SelectedPath = Directory.GetCurrentDirectory();

                DialogResult save = fbdFileExtractMulti.ShowDialog();
                if (save == DialogResult.OK)
                {
                    string extractPath = fbdFileExtractMulti.SelectedPath;
                    List<string> failed_paths = new List<string>(); // TODO: better handling why the operation failed

                    foreach (string file in files)
                    {
                        // Conventional extraction of a file.
                        string outpath = extractPath + Path.DirectorySeparatorChar + Path.GetFileName(file);

                        try
                        {
                            AddonHandle.ExtractFile(file, outpath);
                        }
                        catch (Exception)
                        {
                            failed_paths.Add(file);
                            continue;
                        }
                    }

                    foreach (string folder in subfolders)
                    {
                        // First, build a list of files in the current folder
                        List<string> entriesInFolder = AddonHandle.OpenAddon.Files
                            .Where(f => f.Path.StartsWith(folder)).Select(f => f.Path).ToList();

                        foreach (string entry in entriesInFolder)
                        {
                            // Now it is, yet again the conventional file extraction.
                            // But we have to trim the folder itself from the path to prevent a redundant folder creation
                            // (And that a file is extracted properly but subfolder files
                            // are extracted in a way that their full path is created
                            // when we only want to extract lua/ contents. Like so:
                            // extractPath/a.lua (a file in lua/)
                            // extractPath/lua/subfolder/subfolder/file.lua (a file in subfolder of lua/))
                            string outpath = extractPath + Path.DirectorySeparatorChar + entry.Substring(tvFolders.SelectedNode.Name.Length);

                            try
                            {
                                // We might need to create the directory tree for the extract
                                if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                                    Directory.CreateDirectory(Path.GetDirectoryName(outpath));

                                AddonHandle.ExtractFile(entry, outpath);
                            }
                            catch (Exception)
                            {
                                failed_paths.Add(entry);
                                continue;
                            }
                        }
                    }

                    if (failed_paths.Count == 0)
                        UpdateStatus("Extracted " + countString + " successfully");
                    else if (failed_paths.Count == 1)
                        MessageBox.Show("Failed to extract " + failed_paths[0], "Extract " + verbString,
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    else if (failed_paths.Count > 1)
                    {
                        DialogResult showFailedFiles = MessageBox.Show(countString +
                                " failed to extract.\n\nShow a list of failures?", "Extract " + verbString,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                        if (showFailedFiles == DialogResult.Yes)
                        {
                            string temppath = ContentFile.GenerateExternalPath(
                                    new Random().Next() + "_failedExtracts") + ".txt";

                            try
                            {
                                File.WriteAllText(temppath,
                                    "These files failed to extract:\r\n\r\n" +
                                    String.Join("\r\n", failed_paths.ToArray()));
                            }
                            catch (Exception)
                            {
                                MessageBox.Show("Can't show the list, an error happened generating it.", "Extract " + verbString,
                                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                return;
                            }

                            // The file will be opened by the user's default text file handler (Notepad?)
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(temppath)
                            {
                                UseShellExecute = true,
                            });
                        }
                    }
                }

                fbdFileExtractMulti.Reset();
            }
        }

        private void tsmFileShellExec_Click(object sender, EventArgs e)
        {
            if (lstFiles.SelectedItems.Count == 1)
            {
                if (lstFiles.FocusedItem != null)
                {
                    if ((string)lstFiles.FocusedItem.Tag != "subfolder")
                    {
                        string temppath;
                        try
                        {
                            temppath = Path.GetTempPath() + "/" + Path.GetFileName(lstFiles.FocusedItem.Name);

                            try
                            {
                                File.WriteAllBytes(temppath, AddonHandle.GetFile(lstFiles.FocusedItem.Name).Content);
                            }
                            catch (FileNotFoundException)
                            {
                                MessageBox.Show("The file is not in the archive.", "Shell execute",
                                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                return;
                            }
                            catch (Exception)
                            {
                                MessageBox.Show("Failed to extract file to disk.", "Shell execute",
                                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                return;
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            MessageBox.Show("The file is not in the archive.", "Shell execute",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            return;
                        }

                        // Start the file
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(temppath)
                        {
                            UseShellExecute = true,
                        });
                    }
                }
            }
        }

        private void lstFiles_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void lstFiles_DragDrop(object sender, DragEventArgs e)
        {
            List<string> files = ((String[])e.Data.GetData(DataFormats.FileDrop)).ToList();

            if (files.Count == 0)
            {
                return;
            }
            else if (files.Count == 1 && files[0].EndsWith(".gma"))
            {
                if (files[0].EndsWith(".gma"))
                {
                    DialogResult dropChanges = new DialogResult();
                    if (AddonHandle is RealtimeAddon && AddonHandle.Modified)
                    {
                        dropChanges = MessageBox.Show("Do you want to open another addon without saving the current first?",
                            "An addon is already open", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    }

                    if (dropChanges == DialogResult.Yes || AddonHandle == null ||
                        (AddonHandle is RealtimeAddon && !AddonHandle.Modified))
                    {
                        UnloadAddon();
                        LoadAddon(files[0]);
                    }
                }
            }
            else
            {
                if (files.Any(f => f.EndsWith(".gma")))
                {
                    MessageBox.Show("One or more drag-and-dropped files are GMAs.\nTo load a GMA, only drop one file " +
                        "into SharpGMad.", "Add files",
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                if (!AddonHandle.CanWrite)
                    return;

                List<string> addFailures = new List<string>(files.Count);
                List<string> filesToParse = new List<string>(files); // Create a new list so we can run the foreach below

                foreach (string f in files)
                {
                    if ((File.GetAttributes(f) & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        // The file is in fact a directory, parse the files within and add them
                        foreach (string subfile in Directory.GetFiles(f, "*", SearchOption.AllDirectories))
                        {
                            filesToParse.Add(subfile);
                        }

                        filesToParse.Remove(f);
                    }
                }

                foreach (string f in filesToParse)
                {
                    string filename = f.Replace("\\", "/");

                    try
                    {
                        AddonHandle.OpenAddon.CheckRestrictions(Whitelist.GetMatchingString(filename));
                        AddonHandle.AddFile(Whitelist.GetMatchingString(filename), File.ReadAllBytes(f));
                    }
                    catch (IOException)
                    {
                        addFailures.Add("Error happened while reading " + f);
                        continue;
                    }
                    catch (IgnoredException)
                    {
                        addFailures.Add("File is ignored (" + f + ")");
                        continue;
                    }
                    catch (WhitelistException)
                    {
                        addFailures.Add("File is not whitelisted (" + f + ")");
                        continue;
                    }
                    catch (ArgumentException)
                    {
                        addFailures.Add("Similar file has already been added (" + f + ")");
                        continue;
                    }
                }

                UpdateModified();
                UpdateFolderList();
                UpdateFileList();

                if (addFailures.Count == 0)
                    UpdateStatus("Successfully added " + (filesToParse.Count - addFailures.Count) + " files");
                else if (addFailures.Count == 1)
                    UpdateStatus(addFailures[0]);
                else if (addFailures.Count > 1)
                {
                    UpdateStatus(addFailures.Count + " files failed to add out of " + filesToParse.Count);
                    DialogResult showFailedFiles = MessageBox.Show(addFailures.Count + " files failed to add." +
                        "\n\nShow a list of failures?", "Add files", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (showFailedFiles == DialogResult.Yes)
                    {
                        string temppath = ContentFile.GenerateExternalPath(
                                new Random().Next() + "_failedAdds") + ".txt";

                        try
                        {
                            File.WriteAllText(temppath,
                                "These files failed to add:\r\n\r\n" +
                                String.Join("\r\n", addFailures.ToArray()));
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Can't show the list, an error happened generating it.", "Add files",
                                MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            return;
                        }

                        // The file will be opened by the user's default text file handler (Notepad?)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(temppath)
                        {
                            UseShellExecute = true,
                        });
                    }
                }
            }
        }

        private void tsmiViewElements_changeView(object sender, EventArgs e)
        {
            // Switch the view itself
            if (sender == tsmiViewLargeIcons)
                lstFiles.View = View.LargeIcon;
            else if (sender == tsmiViewSmallIcons)
                lstFiles.View = View.SmallIcon;
            else if (sender == tsmiViewDetails)
                lstFiles.View = View.Details;
            else if (sender == tsmiViewList)
                lstFiles.View = View.List;
            else if (sender == tsmiViewTiles)
                lstFiles.View = View.Tile;

            // Uncheck all view menu entries.
            tsmiViewLargeIcons.Checked = false;
            tsmiViewSmallIcons.Checked = false;
            tsmiViewDetails.Checked = false;
            tsmiViewList.Checked = false;
            tsmiViewTiles.Checked = false;

            // Recheck the one which was clicked, also set the image of the dropdown.
            ((ToolStripMenuItem)sender).Checked = true;
            tsddbViewOptions.Image = ((ToolStripMenuItem)sender).Image;
        }

        private void tsmiViewShowSubfolders_Click(object sender, EventArgs e)
        {
            // Switch the "show" to the opposite value and force update the file list.
            tsmiViewShowSubfolders.Checked = !tsmiViewShowSubfolders.Checked;
            UpdateFileList();
        }
    }
}