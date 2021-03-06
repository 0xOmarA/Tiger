using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Data;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace TigerAudioTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string cashe_location = "packages_path.txt";
        private static string saving_location = "./saved_data";
        private static Dictionary<UInt64, uint> audioHash_referenceHash_lookup = new Dictionary<UInt64, uint>();
        private static DataTable main_table = new DataTable();

        private static string packages_path;
        private static Tiger.Extractor extractor;

        private static NAudio.Wave.WaveOut waveOut = new NAudio.Wave.WaveOut();
        public MainWindow()
        {
            InitializeComponent();
            log("Welcome to TigerAudioTool");
            log("Please keep this tool private");

            log("Initializing the main table");
            main_table.Columns.Add("Audio Hash", typeof(string));
            main_table.Columns.Add("Transcript String", typeof(string));
            main_table.Columns.Add("Narrator", typeof(string));

            Directory.CreateDirectory(saving_location);

            //if the cashe location exists, then use it for the data
            if (System.IO.File.Exists(cashe_location))
            {
                log("Loading packages from the cashed location");
                packages_path = System.IO.File.ReadAllText(cashe_location);
                destiny_path_textbox.Text = packages_path;

                this.Dispatcher.Invoke(() => log("Initializing extractor object"));
                extractor = new Tiger.Extractor(packages_path, Tiger.LoggerLevels.HighVerbouse);
                this.Dispatcher.Invoke(() => log("Extractor initialized"));

                if (File.Exists("file1.adf") && File.Exists("file2.adf"))
                {
                    log("Cache files found. ");
                    log("Loading cashed files.");
                    audioHash_referenceHash_lookup = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<UInt64, uint>>(File.ReadAllText("file2.adf"));
                    Dictionary<string, List<string>> temp_main_table = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(File.ReadAllText("file1.adf"));
                    for(int i = 0; i<temp_main_table["Audio Hash"].Count; i++)
                        main_table.Rows.Add(temp_main_table["Audio Hash"][i], temp_main_table["Transcript String"][i], temp_main_table["Narrator"][i]);
                    main_table_datagrid.DataContext = main_table.DefaultView;
              }
                else
                    initialize();
            }
        }

        /// <summary>
        /// A method used to log data to the logging box
        /// </summary>
        /// <param name="message"></param>
        public void log(string message)
        {
            string time_string = DateTime.Now.ToString("dd/MMM/yyyy hh:mm:ss tt");
            logging_box.Text += $"[{time_string}]: {message}\n";

            logging_box_scroller.ScrollToBottom();
        }

        /// <summary>
        /// A method invoked on when the browse button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void browse_button_onClick(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog file_dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = "Path to destiny2.exe",
                Filter = "Exutables (.exe)|*.exe",
                DefaultExt = ".exe",
                ValidateNames = true,
                CheckPathExists = true,
                CheckFileExists = true,
            };
            
            log("Browse to Destiny2.exe");
            Nullable<bool> result = file_dialog.ShowDialog();

            //only if the user selects a file
            if (result == true)
            {
                string file_path = file_dialog.FileName;
                string file_name = file_path.Split("\\")[^1];
             
                if (file_name.ToUpper() != "DESTINY2.EXE")
                {
                    MessageBox.Show("Please go the path of destiny 2.exe", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    browse_button_onClick(sender, e);
                }

                packages_path = System.IO.Path.Combine(String.Join("\\", file_path.Split("\\")[0..^1]), "packages");
                destiny_path_textbox.Text = packages_path;

                System.IO.File.WriteAllText(cashe_location, packages_path);
                initialize();
            }
        }

    
        /// <summary>
        /// A method used to initialize everything in the data
        /// </summary>
        private void initialize()
        {
            var t = Task.Run(() =>
            {
                this.Dispatcher.Invoke(() => log("Initializing extractor object"));
                extractor = new Tiger.Extractor(packages_path, Tiger.LoggerLevels.HighVerbouse);
                this.Dispatcher.Invoke(() => log("Extractor initialized"));

                this.Dispatcher.Invoke(() => log("Building audio database"));
                foreach(Tiger.Package package in extractor.master_packages_stream())
                {
                    this.Dispatcher.Invoke(() => log($"Analysing {package.no_patch_id_name}"));
                    for(int entry_index = 0; entry_index<package.entry_table().Count;entry_index++)
                    {
                        Tiger.Formats.Entry entry = package.entry_table()[entry_index];
                        if (entry.entry_a != (uint)Tiger.Blocks.Type.AudioBank)
                            continue;

                        this.Dispatcher.Invoke(() => log($"\t|-{Tiger.Utils.entry_name((uint)package.package_id, (uint)entry_index)}"));
                        byte[] parsed_audio_bank_blob = new Tiger.Parsers.AudioBankParser(package, entry_index, extractor).Parse().data;
                        Dictionary<UInt64, Dictionary<string, string>> parsed_audio_bank = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<UInt64, Dictionary<string, string>>>(System.Text.Encoding.UTF8.GetString(parsed_audio_bank_blob));
                        
                        //Iterating over all of the objects in the dictionary
                        foreach(KeyValuePair<UInt64, Dictionary<string, string>> hash_details_pair in parsed_audio_bank)
                        {
                            if(!audioHash_referenceHash_lookup.ContainsKey(hash_details_pair.Key))
                            {
                                audioHash_referenceHash_lookup[hash_details_pair.Key] = Convert.ToUInt32(hash_details_pair.Value["audio reference hash"]);
                                main_table.Rows.Add(Convert.ToUInt64(hash_details_pair.Value["audio hash"]).ToString("X16"), hash_details_pair.Value["transcript string"], hash_details_pair.Value["narrator name"]);
                            }
                        }
                    }
                }

                this.Dispatcher.Invoke(() => log("Caching initialized data"));
                Dictionary<string, List<string>> main_datatable_dict = new Dictionary<string, List<string>>();
                foreach (DataColumn column in main_table.Columns)
                    main_datatable_dict[column.ColumnName] = new List<string>();

                foreach (DataRow row in main_table.Rows)
                    foreach (DataColumn column in main_table.Columns)
                        main_datatable_dict[column.ColumnName].Add(Convert.ToString(row[column.ColumnName]));

                File.WriteAllText("file1.adf", Newtonsoft.Json.JsonConvert.SerializeObject(main_datatable_dict));
                File.WriteAllText("file2.adf", Newtonsoft.Json.JsonConvert.SerializeObject(audioHash_referenceHash_lookup));

                this.Dispatcher.Invoke(() => log("Initialization complete"));
                this.Dispatcher.Invoke(() => main_table_datagrid.DataContext = main_table.DefaultView);
                this.Dispatcher.Invoke(() => main_table_datagrid.Items.Refresh());
            });
        }

        private void table_loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                main_table_datagrid.Columns[0].Width = new DataGridLength(120, DataGridLengthUnitType.Pixel);
                main_table_datagrid.Columns[1].Width = new DataGridLength(544, DataGridLengthUnitType.Pixel);
                main_table_datagrid.Columns[2].Width = new DataGridLength(100, DataGridLengthUnitType.Pixel);
            }
            catch { }
        }

        /// <summary>
        /// Simple method that takes in the ogg data as byte array and plays it
        /// </summary>
        /// <param name="OggData">A byte array containing the ogg data to be played</param>
        private void PlayOgg(byte[] OggData)
        {
            var vorbis = new NAudio.Vorbis.VorbisWaveReader(new MemoryStream(OggData));

            try
            {
                //waveOut.Dispose();
                waveOut.Init(vorbis);
                waveOut.Play();
            }
            catch (Exception ex)
            {
                log("Error Playing the audio");
            }
        }

        /// <summary>
        /// A method invoked when you double click inside a cell
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void main_table_datagrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int selection_index = main_table_datagrid.SelectedIndex;
            var x = main_table.Rows[selection_index];

            //Write it to the log box
            log($"{x["Narrator"]}: {x["Transcript String"]}");

            UInt64 audio_hash = Convert.ToUInt64(x["Audio Hash"].ToString(), 16);
            Tiger.Utils.EntryReference reference_hash = new Tiger.Utils.EntryReference(audioHash_referenceHash_lookup[audio_hash]);

            byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(reference_hash, extractor).Parse().data;
            PlayOgg(ogg_data);

            if(save_played_checkbox.IsChecked == true)
            {
                File.WriteAllBytes(System.IO.Path.Combine(saving_location, x["Audio Hash"].ToString() + ".ogg"), ogg_data);
                File.WriteAllText(System.IO.Path.Combine(saving_location, x["Audio Hash"].ToString() + ".txt"), $"{x["Narrator"]}: {x["Transcript String"]}");
                log($"Audio data written to {saving_location}");
            }
        }

        private void reinitialize_button_onClick(object sender, RoutedEventArgs e)
        {
            log("Reinitialization process beginning");
            initialize();
        }

        private void deleteCache_button_onClick(object sender, RoutedEventArgs e)
        {
            log("Removing all cache files");
            File.Delete("file1.adf");
            File.Delete("file2.adf");
            File.Delete(cashe_location);

            DataTable x = new DataTable();
            x.Columns.Add("Audio Hash", typeof(string));
            x.Columns.Add("Transcript String", typeof(string));
            x.Columns.Add("Narrator", typeof(string));
            main_table_datagrid.DataContext = x.DefaultView;

            log("Caches removed");
        }
    }
}
