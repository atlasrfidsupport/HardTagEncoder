using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using ThingMagic;
using Microsoft.Office.Interop;
using System.Windows.Forms;



namespace HardTagEncoder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        

        public MainWindow()
        {
            InitializeComponent();

            
        }

        //variables
        public static String uriString = "";
        public static String comPort = "";
        public static String ipAddress = "";
        public static String messageText = "";
        public static int duration = 1000;
        public TagReadData[] tagData;
        public static string[] comPorts;
        public static int api = 1;  // use the Mercury API by default
        public Reader JadakReader;
        public static int readerMode;
        public static Microsoft.Office.Interop.Excel.Application xlApp;
        public static Microsoft.Office.Interop.Excel.Workbook xlWorkBook;
        public static Microsoft.Office.Interop.Excel.Worksheet xlWorkSheet;
        public static Microsoft.Office.Interop.Excel.Range range;
        public bool isDatabaseUsed = false;
        



        public void scanCOM()
        {
             comPorts = System.IO.Ports.SerialPort.GetPortNames();
            if(comPorts.Length == 0)
            {
                reportText.Text = "No Serial Devices Found";
            }
             foreach (string port in comPorts)
            {
                reportText.Text = "Available COM ports: \n\n";
                reportText.AppendText(port + "\n");
                addressText.Text = port;
            }
        }

        public void scanIP()
        {

        }

        // Mercury API methods ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public Reader createJadakReader(string uriString)
        {
            
            try
            {              
                JadakReader = Reader.Create(uriString);
                JadakReader.ParamSet("/reader/region/id", Reader.Region.NA);
                return JadakReader;
            }
            catch (Exception)
            {
                reportText.Text = "Please select reader manufacturer and connection type.";                
            }
            return JadakReader;
              
        }

        public void connectJadakReader(Reader reader)
        {
            reportText.Text = "Connecting reader...";
            try
            {
                reader.Connect();
                reportText.Text = "Reader Connected";
                setJadakReadPower(reader, 1000);
            }
            catch(ReaderException e)
            {
                reportText.Text = "Unable to connect to reader; Error: " + e.ToString();
            }
            catch(System.UnauthorizedAccessException ex)
            {
                reportText.Text = "Access to Port Denied; This port is already in use, or the reader is already connected";
            }
            catch(System.IO.IOException ioe)
            {
                messageText = "Error: " + ioe.Message.ToString();
                reportText.Text = "Error: " + ioe.Message.ToString() + ". Please ensure the reader is properly connected.";
            }
            
            
        }

        public void destroyJadakReader(Reader reader)
        {
            reader.Destroy();
        }

        public void setJadakReaderMode(Reader reader, int mode)
        {
            reader.ParamSet("/reader/gen2/tagEncoding", mode);
        }

        public void setJadakSearchMode(Reader reader, int mode)
        {
            reader.ParamSet("/reader/gen2/target", mode);
        }

        public void setJadakSession(Reader reader, int session)
        {
            reader.ParamSet("/reader/gen2/session", session);
        }

        public void setJadakRegion(Reader reader, int region)
        {
            reader.ParamSet("/reader/region/id", region);
        }

        public void setJadakReadPower(Reader reader, int powerInDb)
        {
            reader.ParamSet("/reader/radio/readPower", powerInDb);
        }

        public void setJadakWritePower(Reader reader, int powerInDb)
        {
            reader.ParamSet("/reader/radio/writePower", powerInDb);
        }

        public void setJadakReadDuration(int durationMsec)
        {
            duration = durationMsec;
        }

        public string readJadakSingleTag(Reader reader, int duration)
        {          
            tagData = reader.Read(duration);
            if(tagData.Length > 1)
            {
                messageText = "More than 1 tag read";
                return messageText;
            }
            else if(tagData.Length != 0)
            {
                if (hexRadio.IsChecked == true)
                {
                    return tagData[0].EpcString;
                }
                else
                {
                    byte[] ba = Encoding.Default.GetBytes(tagData[0].EpcString);
                    string asciiString = BitConverter.ToString(ba).Replace("-","");
                    return asciiString;
                }
                
            }
            else
            {
                messageText = "No tags found";
                return messageText;
            }
        }

        public string readJadakUserMemory(Reader reader)
        {
            try
            {
                var userReadSuccess = reader.ReadTagMemBytes(null, 3, 0, 24);
                string hex = BitConverter.ToString(userReadSuccess).Replace("-", "");
                return hex;
            }
            catch (Exception e)
            {
                reportText.Text = e.Message.ToString();
                throw;
            }
            
        }

        public string readJadakTIDMemory(Reader reader)
        {
            var tidReadSuccess = reader.ReadTagMemBytes(null, 2, 0, 24);
            string hex = BitConverter.ToString(tidReadSuccess).Replace("-", "");
            return hex;
        }

        public bool writeJadakTag(Reader reader, TagFilter filter, Gen2.WriteTag tagOp)
        {         

            try
                {
                    reader.ExecuteTagOp(tagOp, filter);
                    reportText.Text = "Tag Successfully Written: " + tagOp.Epc.ToString();
                    return true;
                }
                catch (ReaderException e)
                {
                    reportText.Text = "Unable to write tag; Error: " + e.ToString();
                    return false;
                }
   
        }

        public bool writeJadakTagUser(Reader reader, TagFilter filter, Gen2.WriteData tagOp)
        {
            try
            {
                reader.ExecuteTagOp(tagOp, filter);
                reportText.Text = "User Memory Written";
                return true;
            }
            catch (ReaderException e)
            {
                messageText = "Unable to write tag; Error: " + e.ToString();
                return false;
            }
        }

       
        



        // Octane API Methods /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////






        // GUI Interactions ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void writeButton_Click(object sender, RoutedEventArgs e)
        {
            
            if (api == 1)
            {
                
                
                TagFilter filter = null;
                string epc = writeText.Text;
                var strs = Enumerable.Range(0,epc.Length/2).Select(i=>epc.Substring(i*2,2));
                byte[] epcBytes = new byte[strs.ToString().Length];

                try
                {
                    epcBytes = strs.Select(s => Convert.ToByte($"0x{s}", 16)).ToArray();
                }
                catch (System.FormatException err)
                {
                    reportText.Text = "No Tag Writes; Error: " + err.Message;
                    reportText.Background = Brushes.Yellow;
                    System.Threading.Thread.Sleep(500);
                    reportText.Background = Brushes.Red;
                    //writeText.Text = "";
                    writeText.Focus();
                }
                 
                
                Gen2.TagData epcData = new Gen2.TagData(epcBytes);
                Gen2.WriteTag tagOp;

                if(userRadioWrite.IsChecked == false)
                {
                    tagOp = new Gen2.WriteTag(epcData);
                    try
                    {
                        var success = writeJadakTag(JadakReader, filter, tagOp);
                        if (success == true)
                        {
                            messageText = "Write Successful";
                            reportText.Background = Brushes.Yellow;
                            System.Threading.Thread.Sleep(500);
                            reportText.Background = Brushes.Green;
                            writeText.Text = "";
                            writeText.Focus();
                            if (isDatabaseUsed == true)
                            {
                                var databaseSelector = int.Parse(databaseRecordText.Text) + 1;
                                databaseRecordText.Text = databaseSelector.ToString();
                                updateExcelData(xlWorkSheet, databaseSelector);
                            }
                        }
                        else
                        {
                            messageText = "No Tag Writes";
                            reportText.Background = Brushes.Yellow;
                            System.Threading.Thread.Sleep(500);
                            reportText.Background = Brushes.Red;
                            //writeText.Text = "";
                            writeText.Focus();
                        }


                    }
                    catch (FAULT_PROTOCOL_WRITE_FAILED_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }
                    catch (FAULT_WRITE_PASSED_LOCK_FAILED_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }
                    catch (FAULT_GEN2_PROTOCOL_MEMORY_LOCKED_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }
                    catch (FAULT_GEN2_PROTOCOL_MEMORY_OVERRUN_BAD_PC_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }
                    catch (FAULT_NO_TAGS_FOUND_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }
                }
                else
                {
                    ushort[] userData = new ushort[writeText.Text.Length];
                    int j = 0;
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(epcBytes);
                    }
                    for (int i = 0; i < epcBytes.Length - 1; i = i+2)
                    {
                        
                        userData[j] = BitConverter.ToUInt16(epcBytes, i);
                        j++;
                    }
                    Gen2.WriteData tagOpUser = new Gen2.WriteData(Gen2.Bank.USER, 0, userData);
                    
                    try
                    {
                        var success = writeJadakTagUser(JadakReader, filter, tagOpUser);
                        if (success == true)
                        {
                            messageText = "Write Successful";
                            reportText.Background = Brushes.Yellow;
                            System.Threading.Thread.Sleep(500);
                            reportText.Background = Brushes.Green;
                            writeText.Text = "";
                            writeText.Focus();
                            if (isDatabaseUsed == true)
                            {
                                var databaseSelector = int.Parse(databaseRecordText.Text) + 1;
                                databaseRecordText.Text = databaseSelector.ToString();
                                updateExcelData(xlWorkSheet, databaseSelector);
                            }
                        }
                        else
                        {
                            reportText.Text = "No Tag Writes";
                            reportText.Background = Brushes.Yellow;
                            System.Threading.Thread.Sleep(500);
                            reportText.Background = Brushes.Red;                           
                            writeText.Focus();
                        }
                    }
                    catch (FAULT_PROTOCOL_WRITE_FAILED_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }
                    catch (FAULT_WRITE_PASSED_LOCK_FAILED_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }
                    catch (FAULT_GEN2_PROTOCOL_MEMORY_LOCKED_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }
                    catch (FAULT_GEN2_PROTOCOL_MEMORY_OVERRUN_BAD_PC_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }
                    catch (FAULT_NO_TAGS_FOUND_Exception ex)
                    {
                        System.Windows.MessageBox.Show("No Tag Writes, Please Scan Again; Error: " + ex.Message.ToString());
                        reportText.Background = Brushes.Yellow;
                        System.Threading.Thread.Sleep(500);
                        reportText.Background = Brushes.Red;
                        //writeText.Text = "";
                    }

                }
                
                




                //check if tag is being locked
                if ((bool)lockRadioButton.IsChecked)
                {
                    if(passwordText.Text.Length == 0)
                    {
                        System.Windows.MessageBox.Show("Please provide an Access Password to lock tags");
                    }
                    else
                    {
                        uint password = 0x41022400;
                        ushort[] array = { 0x4102, 0x2400 };

                        Gen2.WriteData accessPWDTagOp = new Gen2.WriteData(Gen2.Bank.RESERVED, 2, array);
                        Gen2.Lock lockTagOp = new Gen2.Lock(password, Gen2.LockAction.EPC_LOCK);
                        try
                        {
                            JadakReader.ExecuteTagOp(accessPWDTagOp, null);
                            JadakReader.ExecuteTagOp(lockTagOp, null);
                            messageText = "Lock Successful";
                        }
                        catch (ReaderException ex)
                        {
                            System.Windows.MessageBox.Show("Lock Tag failed; Error: " + ex.ToString());
                        }
                    }
                    

                }

                //check if tag is being unlocked
                if (unlockRadioButton.IsChecked == true)
                {
                    if (passwordText.Text.Length == 0)
                    {
                        System.Windows.MessageBox.Show("Please provide the Access Password to unlock tags");
                    }
                    else
                    {
                        uint password = 0x41022400;
                        ushort[] array = { 0x4102, 0x2400 };
                        Gen2.WriteData accessPWDTagOp = new Gen2.WriteData(Gen2.Bank.RESERVED, 2, array);
                        Gen2.Lock lockTagOp = new Gen2.Lock(password, Gen2.LockAction.EPC_UNLOCK);
                        try
                        {
                            JadakReader.ExecuteTagOp(accessPWDTagOp, null);
                            JadakReader.ExecuteTagOp(lockTagOp, null);
                            messageText = "Unlock Successful";
                        }
                        catch (ReaderException ex)
                        {
                            System.Windows.MessageBox.Show("Lock Tag failed; Error: " + ex.ToString());
                        }
                    }

                    
                }

            }

            
            
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            if(api == 1)
            {
                if((string)connectionCombo.SelectionBoxItem == "Serial")
                {
                    comPort = addressText.Text;
                    uriString = "tmr:///" + comPort;                  
                }
                else if ((string)connectionCombo.SelectionBoxItem == "Network")
                {
                    ipAddress = addressText.Text;
                    uriString = "tmr:///" + ipAddress;
                }

                JadakReader = createJadakReader(uriString);
                if(JadakReader != null)
                {
                    connectJadakReader(JadakReader);
                }
                else
                {
                    reportText.Text = "Please select a reader manufacturer and connection method.";
                }
                
            }
        }

        private void scanButton_Click(object sender, RoutedEventArgs e)
        {
            scanCOM();
        }

        private void readButton_Click(object sender, RoutedEventArgs e)
        {
            if(tidRadio.IsChecked == true)
            {
                try
                {
                    readText.Text = readJadakTIDMemory(JadakReader);
                }
                catch (System.NullReferenceException ex)
                {
                    reportText.Text = "Please connect a reader";

                }
                
            }
            else if(userRadio.IsChecked == true)
            {
                try
                {
                    readText.Text = readJadakUserMemory(JadakReader);
                }
                catch (System.NullReferenceException ex)
                {
                    reportText.Text = "Please connect a reader";

                }
                
            }
            else
            {
                try
                {
                    readText.Text = readJadakSingleTag(JadakReader, duration);
                }
                catch (System.NullReferenceException ex)
                {
                    reportText.Text = "Please connect a reader";
                    
                }
                
            }
            
        }

        private void readerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void manufacturerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /*if(manufacturerCombo.SelectedIndex == 0)
            {
                readerCombo.Items.Add("USB Pro");
                readerCombo.Items.Add("Sargas");
                readerCombo.Items.Add("IZAR");
            }*/
        }

        private void configureButton_Click(object sender, RoutedEventArgs e)
        {
            ReaderSettings p = new ReaderSettings();
            p.Show();
        }

        private void writeText_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == Key.Enter || e.Key == Key.Space)
            {
                writeButton_Click(sender, e);
            }
            
        }

        private void lockRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            
            
        }

        private void writePowerText_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void readerModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           if(readerModeCombo.SelectedIndex == 0)
            {
                readerMode = 1;
            }
           else if(readerModeCombo.SelectedIndex == 1)
            {
                readerMode = 2;
            }
            else if (readerModeCombo.SelectedIndex == 2)
            {
                readerMode = 4;
            }
            else if (readerModeCombo.SelectedIndex == 3)
            {
                readerMode = 8;
            }
        }

        private void writePowerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            
        }

        private void readPowerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            
        }

        private void saveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            setJadakReadPower(JadakReader, (int)readPowerSlider.Value * 100);
            setJadakWritePower(JadakReader, (int)writePowerSlider.Value * 100);
            configureExpand.IsExpanded = false;
        }

        private void databaseButton_Click(object sender, RoutedEventArgs e)
        {
            var fileContent = string.Empty;
            var filePath = string.Empty;
            var dataSelector = int.Parse(databaseRecordText.Text);

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "Excel files (*.xls;*.xlsx)|*.xls;*.xlsx|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.FileName = filePath;

                openFileDialog.ShowDialog();

                openFileDialog.FileOk += OpenFileDialog_FileOk;

                //Get the path of specified file
                filePath = openFileDialog.FileName;
                filePathText.Text = filePath;
                databaseRecordText.Text = "1";

                //read the contents of the Excel file according to the database record number field
                xlApp = new Microsoft.Office.Interop.Excel.Application();
                try
                {
                    xlWorkBook = xlApp.Workbooks.Open(filePath);
                    xlWorkSheet = xlWorkBook.ActiveSheet;
                    range = xlWorkSheet.UsedRange;

                    //update the writeText object with the selected Excel cell's data
                    updateExcelData(xlWorkSheet, dataSelector);

                    isDatabaseUsed = true;
                }
                catch (System.Runtime.InteropServices.COMException er)
                {
                    reportText.Text = "Database Selection Cancelled";
                }
                

            }

        }

        private void OpenFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
            throw new NotImplementedException();
        }

        public void updateExcelData(Microsoft.Office.Interop.Excel.Worksheet xlWorksheet, int dataSelector)
        {
            writeText.Text = xlWorkSheet.Cells[dataSelector + 1, 1].value;
        }

        private void databaseRecordText_TextChanged(object sender, TextChangedEventArgs e)
        {
            
        }

        private void updateDbButton_Click(object sender, RoutedEventArgs e)
        {
            updateExcelData(xlWorkSheet, int.Parse(databaseRecordText.Text));
        }

        private void helpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //open Help page
                System.Diagnostics.Process.Start("url");
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("Unable to open web page. Please check your internet connection.");
                
            }
            
        }
    }
}
