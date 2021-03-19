namespace FileStub.Templates
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Runtime.Remoting.Messaging;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Newtonsoft.Json;
    using RTCV.Common;
    using RTCV.CorruptCore;
    using RTCV.UI;

    public partial class FileStubTemplateYuzu : Form, IFileStubTemplate
    {

        const string YUZUSTUB_MAIN = "Yuzu : NS Executable - main";
        const string YUZUSTUB_ALL = "Yuzu : NS Executables - main, sdk, and subsdk";
        public string YuzuDir = Path.Combine(FileStub.FileWatch.currentDir, "YUZU");
        public string NSNSOTOOLPATH ;
        public string YuzuExePath;
        YuzuTemplateSession currentYuzuSession;
        public Dictionary<string, YuzuTemplateSession> knownGamesDico = new Dictionary<string, YuzuTemplateSession>();
        string currentSelectedTemplate = null;
        Process YuzuProcess = null;
        string gamepath = null;
        public string[] TemplateNames { get => new string[] {
            YUZUSTUB_MAIN,
            YUZUSTUB_ALL,
        }; }

        public bool DisplayDragAndDrop => true;
        public bool DisplayBrowseTarget => true;


        public FileStubTemplateYuzu()
        {
            InitializeComponent();
            if (!Directory.Exists(YuzuDir))
                Directory.CreateDirectory(YuzuDir);

            string YuzuParamsDir = Path.Combine(YuzuDir, "PARAMS");

            if (!Directory.Exists(YuzuParamsDir))
                Directory.CreateDirectory(YuzuParamsDir);
            lbNSOTarget.Visible = false;
            NSNSOTOOLPATH = Path.Combine(YuzuDir, "nsnsotool.exe");
            currentYuzuSession = new YuzuTemplateSession();
        }
        public FileTarget[] GetTargets()
        {
            string targetExe = lbNSOTarget.Text;

            if (targetExe == "")
            {
                MessageBox.Show("No target loaded");
                return null;
            }

            List<FileTarget> targets = new List<FileTarget>();

            var exeFileInfo = new FileInfo(targetExe);
            var exeFolder = exeFileInfo.Directory.FullName;

            var baseFolder = exeFileInfo.Directory;


            List<FileInfo> allFiles = SelectMultipleForm.DirSearch(baseFolder);

            string baseless(string path) => path.Replace(exeFolder, "");

            var exeTarget = Vault.RequestFileTarget(baseless(exeFileInfo.FullName), baseFolder.FullName);

            //var allDlls = allFiles.Where(it => it.Extension == ".dll");

            var allExecutables = allFiles.Where(it =>
                    it.Name.ToUpper().Contains("MAIN") && !it.Name.ToUpper().Contains("NPDM") && !it.Name.ToUpper().Contains("BAK") ||
                    it.Name.ToUpper().Contains("SDK")
                    ).ToArray();

            var allMain = allExecutables.Where(it =>
                    it.Name.ToUpper().Contains("MAIN")
                    ).ToArray();


            switch (currentSelectedTemplate)
            {
                case YUZUSTUB_MAIN:
                    {
                        targets.Add(exeTarget);
                    }
                    break;
                case YUZUSTUB_ALL:
                    {
                        targets.AddRange(allExecutables.Select(it => Vault.RequestFileTarget(baseless(it.FullName), baseFolder.FullName)));
                    }
                    break;
            }
            gamepath = targetExe;
            currentYuzuSession.gameName = exeFileInfo.Directory.Name;
            lbGameName.Visible = false;
            knownGamesDico[currentYuzuSession.gameName] = currentYuzuSession;
            cbSelectedGame.Items.Add(currentYuzuSession.gameName);
            cbSelectedGame.SelectedIndex = cbSelectedGame.Items.Count - 1;
            currentYuzuSession.gameMainExePath = gamepath;
            foreach (YuzuTemplateSession cgi in knownGamesDico.Values)
            {

                cgi.YuzuExePath = currentYuzuSession.YuzuExePath;
                cgi.gameMainExePath = currentYuzuSession.gameMainExePath;
            }
            SaveKnownGames();

            //Prepare filestub for execution
            var sf = S.GET<StubForm>();
            FileWatch.currentSession.selectedExecution = ExecutionType.EXECUTE_OTHER_PROGRAM;
            Executor.otherProgram = currentYuzuSession.YuzuExePath;
            sf.tbArgs.Text = $"\"{currentYuzuSession.gameMainExePath}\"";
            
            return targets.ToArray();
        }

        public Form GetTemplateForm(string name)
        {
            this.SummonTemplate(name);
            return this;
        }

        public bool LoadKnownGames()
        {
            JsonSerializer serializer = new JsonSerializer();
            string path = Path.Combine(YuzuDir, "PARAMS", "knowngames.json");
            if (!File.Exists(path))
            {
                knownGamesDico = new Dictionary<string, YuzuTemplateSession>();
                return true;
            }
            try
            {

                using (StreamReader sw = new StreamReader(path))
                using (JsonTextReader reader = new JsonTextReader(sw))
                {
                    knownGamesDico = serializer.Deserialize<Dictionary<string, YuzuTemplateSession>>(reader);
                }

                foreach (var key in knownGamesDico.Keys)
                    cbSelectedGame.Items.Add(key);
            }
            catch (IOException e)
            {
                MessageBox.Show("Unable to access the filemap! Figure out what's locking it and then restart the WGH.\n" + e.ToString());
                return false;
            }
            return true;
        }
        public bool SaveKnownGames()
        {
            JsonSerializer serializer = new JsonSerializer();
            var path = Path.Combine(YuzuDir, "PARAMS", "knowngames.json");
            try
            {
                using (StreamWriter sw = new StreamWriter(path))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, knownGamesDico);
                }
            }
            catch (IOException e)
            {
                MessageBox.Show("Unable to access the known games!\n" + e.ToString());
                return false;
            }
            return true;
        }
        private void SummonTemplate(string name)
        {
            currentSelectedTemplate = name;

            lbTemplateDescription.Text =
$@"== Corrupt Switch Games ==
Click on Select Yuzu and select the location of your version of Yuzu you wish to use, then...
Click on Browse Target and select the main executable of the game you want to corrupt or drag it into the box.
";
        }

        bool IFileStubTemplate.DragDrop(string[] fd)
        {
            if(fd.Length > 1 || fd[0].EndsWith("\\") || !fd[0].ToUpper().Contains("MAIN"))
            {
                MessageBox.Show("Please only drop the game's main executable");
                lbNSOTarget.Text = "";
                return false;
            }

            lbNSOTarget.Text = fd[0];
            return true;
        }

        public void BrowseFiles()
        {
            string filename;

            OpenFileDialog OpenFileDialog1;
            OpenFileDialog1 = new OpenFileDialog();

            OpenFileDialog1.Title = "Open Main Executable";
            OpenFileDialog1.Filter = "main executable|main";
            OpenFileDialog1.RestoreDirectory = true;
            if (OpenFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (OpenFileDialog1.FileName.ToString().Contains('^'))
                {
                    MessageBox.Show("You can't use a file that contains the character ^ ");
                    lbNSOTarget.Text = "";
                    return;
                }

                filename = OpenFileDialog1.FileName;
            }
            else
            {
                lbNSOTarget.Text = "";
                return;
            }
            lbNSOTarget.Text = filename;
        }

        public void GetSegments(FileInterface exeInterface)
        {
            NSOHelper nso = new NSOHelper(exeInterface);
            FileInfo fileInfo = new FileInfo(exeInterface.Filename);
            long[] coderange = new long[2];
            coderange[0] = nso.codeoffset;
            coderange[1] = nso.codeoffset + nso.codesize;
            if (coderange[0] >= coderange[1])
                return;
            string codevmdnametext = fileInfo.Name + "|code";
            List<long[]> coderanges = new List<long[]>();
            coderanges.Add(coderange);
            VmdPrototype code = new VmdPrototype();
            code.GenDomain = exeInterface.ToString();
            code.BigEndian = exeInterface.BigEndian;
            code.AddRanges = coderanges;
            code.WordSize = exeInterface.WordSize;
            code.VmdName = codevmdnametext;
            code.PointerSpacer = 1;
            RTCV.NetCore.LocalNetCoreRouter.Route(RTCV.NetCore.Endpoints.CorruptCore, RTCV.NetCore.Commands.Remote.DomainVMDAdd, (object)code, true);
            long[] rodatarange = new long[2];
            rodatarange[0] = nso.rodataoffset;
            rodatarange[1] = nso.rodataoffset + nso.rodatasize;
            if (rodatarange[0] >= rodatarange[1])
                return;
            string rodatavmdnametext = fileInfo.Name + "|read-only data";
            List<long[]> rodataranges = new List<long[]>();
            rodataranges.Add(rodatarange);
            VmdPrototype rodata = new VmdPrototype();
            rodata.GenDomain = exeInterface.ToString();
            rodata.BigEndian = exeInterface.BigEndian;
            rodata.AddRanges = rodataranges;
            rodata.WordSize = exeInterface.WordSize;
            rodata.VmdName = rodatavmdnametext;
            rodata.PointerSpacer = 1;
            RTCV.NetCore.LocalNetCoreRouter.Route(RTCV.NetCore.Endpoints.CorruptCore, RTCV.NetCore.Commands.Remote.DomainVMDAdd, (object)rodata, true);
            long[] rwdatarange = new long[2];
            rwdatarange[0] = nso.rwdataoffset;
            rwdatarange[1] = nso.rwdataoffset + nso.rwdatasize;
            if (rwdatarange[0] >= rwdatarange[1])
                return;
            string rwdatavmdnametext = fileInfo.Name + "|data";
            List<long[]> rwdataranges = new List<long[]>();
            rwdataranges.Add(rwdatarange);
            VmdPrototype rwdata = new VmdPrototype();
            rwdata.GenDomain = exeInterface.ToString();
            rwdata.BigEndian = exeInterface.BigEndian;
            rwdata.AddRanges = rwdataranges;
            rwdata.WordSize = exeInterface.WordSize;
            rwdata.VmdName = rwdatavmdnametext;
            rwdata.PointerSpacer = 1;
            RTCV.NetCore.LocalNetCoreRouter.Route(RTCV.NetCore.Endpoints.CorruptCore, RTCV.NetCore.Commands.Remote.DomainVMDAdd, (object)rwdata, true);
            S.GET<VmdPoolForm>().RefreshVMDs();
            S.GET<MemoryDomainsForm>().RefreshDomains();
        }

        private void btnEditExec_Click(object sender, EventArgs e)
        {

            string filename = "";

            OpenFileDialog OpenFileDialog1;
            OpenFileDialog1 = new OpenFileDialog();

            OpenFileDialog1.Title = "Open Yuzu";
            OpenFileDialog1.Filter = "Yuzu|yuzu.exe";
            OpenFileDialog1.RestoreDirectory = true;
            if (OpenFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (OpenFileDialog1.FileName.ToString().Contains('^'))
                {
                    MessageBox.Show("You can't use a file that contains the character ^ ");
                    //lbNSOTarget.Text = "";
                    return;
                }

                filename = OpenFileDialog1.FileName;
            }
            YuzuExePath = filename;
            currentYuzuSession.YuzuExePath = filename;
        }

        private void btnDecompress_Click(object sender, EventArgs e)
        {
            OpenFileDialog OpenFileDialog1;
            OpenFileDialog1 = new OpenFileDialog();

            OpenFileDialog1.Title = "Open Switch Executable";
            OpenFileDialog1.Filter = "main|main|sdk|sdk|subsdk0|subsdk0|subsdk1|subsdk1|subsdk2|subsdk2|subsdk3|subsdk3";
            OpenFileDialog1.RestoreDirectory = true;
            string args;
            if (OpenFileDialog1.ShowDialog() == DialogResult.OK)
            {
                File.Copy(OpenFileDialog1.FileName, OpenFileDialog1.FileName + ".bak");
                File.Delete(OpenFileDialog1.FileName);
                args = "\"" + OpenFileDialog1.FileName + ".bak\" \"" + OpenFileDialog1.FileName + "\"";
                Process.Start(NSNSOTOOLPATH, args);
            }
        }

        private void btnGetSegments_Click(object sender, EventArgs e)
        {
            foreach (var fi in (FileWatch.currentSession.fileInterface as MultipleFileInterface).FileInterfaces)
                GetSegments(fi);
        }
    }
    public class YuzuTemplateSession
    {
        public FileInfo gameMainExeFileInfo = null;
        public FileInfo YuzuExeFile = null;
        public string YuzuExePath = null;
        public DirectoryInfo gameSaveFolder = null;
        public string mainExeFile = null;
        public string gameMainExePath = null;
        public string FirstID = null;
        public string SecondID = null;
        public string fileInterfaceTargetId = null;
        public string gameName = "Autodetect";
        public string mainUncompressedToken = null;
        internal FileMemoryInterface fileInterface;

        public override string ToString()
        {
            return gameName;
        }
    }
}
