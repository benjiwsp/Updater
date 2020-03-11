using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;
using System.Diagnostics;
using System.Configuration;

namespace Updater
{
    class Updater
    {

        static string connStr = "Server=mydbinstance.c7pvwaixaizr.ap-southeast-1.rds.amazonaws.com;Port=3306;Database=CashPOSDB;Uid=root;Pwd=SFAdmin123;charset=utf8; allow zero datetime=true";
        static MySqlCommand myCommand;
        static MySqlConnection myConnection = new MySqlConnection(connStr);
        static MySqlDataReader rdr;
        static string dlZip;
        static string unZipedFolder;
        public Updater()
        {
        }

        static private void Connect()
        {
            try
            {
                myConnection.Open();

            }
            catch (Exception ex)
            {
                Console.WriteLine("already connected to server...");
            }
            finally
            {
                myConnection.Close();
                myConnection.Open();
            }
        }
        static private void Dc()
        {
            try
            {
                myConnection.Close();

            }
            catch (Exception ex)
            {
                Console.WriteLine("already disconnected from server...");
            }
        }
        static private bool CheckVersion()
        {
            bool reVal = false;
            decimal updatedVer = 0.0m;

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string erpPath = @"\Release\Versions\version.txt";
            string path = desktop + erpPath;
            //string[] version = Directory.GetFiles(desktop + erpPath);
            Console.WriteLine(desktop);
            Console.WriteLine(File.Exists(desktop + erpPath));
            StreamReader sr = new StreamReader(path);
            decimal currVer = Convert.ToDecimal(sr.ReadLine());
            sr.Close();
            Connect();

            myCommand = new MySqlCommand("Select Version from CashPOSDB.version", myConnection);
            rdr = myCommand.ExecuteReader();
            if (rdr.HasRows)
            {
                if (rdr.Read())
                {
                    updatedVer = Convert.ToDecimal(rdr["Version"].ToString());
                }
            } rdr.Close();
            Dc();

            if (currVer < updatedVer)
            {
                Console.WriteLine("needs update...");
                reVal = true;
                //     Console.ReadLine();
            }
            return reVal;
        }
        [STAThread]
        static void Main(string[] args)
        {
            //downloadUpdate();
            uploadNewVersion();
          //  Console.Read();
        }
        static void downloadUpdate()
        {
            /*   
             * check updates and run POS;
             */
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string POS = desktop + @"\Release\CashPOS.exe";
            if (CheckVersion())
            {
                unZip(download());
                deleteZip(dlZip);
                Console.WriteLine("Finished update...");
                run(POS);
            }
            else
            {
                Console.WriteLine("There is no updates...");
                run(POS);
            }
        }

        static void uploadNewVersion()
        {
            /*
             * upload new version 
             */
            if (zip())
                upload(@"E:\Development\CashPOS\CashPOS\CashPOS\bin\Release.zip");
            Console.ReadLine();

        }
        static void run(string path)
        {
            Process.Start(path);

        }
        static bool zip()
        {
            string startPath = "";
            string zipPath = "";

            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                startPath = fbd.SelectedPath;
                zipPath = Directory.GetParent(fbd.SelectedPath) + @"\Release.zip";
            }
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            ZipFile.CreateFromDirectory(startPath, zipPath);
            Console.WriteLine("zip completed...");
            // ZipFile.ExtractToDirectory(zipPath, extractPath);
            return true;
        }
        static void upload(string file)
        {
            string extractPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            // string filePath = extractPath + @"\Release.zip";
            string filePath = file;
            Connect();
            myCommand = new MySqlCommand("delete  from CashPOSDB.ProgramFile", myConnection);
            myCommand.ExecuteNonQuery();
            Dc();

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand())
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    cmd.CommandText = @"INSERT INTO CashPOSDB.ProgramFile(ID, ZipFile, FileSize) VALUES (@id, @file, @size)";
                    cmd.Parameters.AddWithValue("@id", 1); // some way of identifying file
                    cmd.Parameters.AddWithValue("@file", bytes);
                    cmd.Parameters.AddWithValue("@size", bytes.Length);
                    cmd.Connection = conn;
                    conn.Open();
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
            Dc();


            StreamReader sr = new StreamReader(@"E:\Development\CashPOS\CashPOS\CashPOS\bin\Release\Versions\version.txt");
            decimal currVer = Convert.ToDecimal(sr.ReadLine());
            sr.Close();
            myCommand = new MySqlCommand("update CashPOSDB.version set Version = '" + currVer + "'", myConnection);
            Connect();
            myCommand.ExecuteNonQuery();
            Dc();
            Console.WriteLine("upload completed...");
        }
        static string download()
        {
            Console.WriteLine("downloading updates...");
            byte[] rawData;
            FileStream fs;

            string extractPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            string zipPath = extractPath + @"\Release.zip";
            string fileName = zipPath;
            dlZip = fileName;
            string u = "SELECT * from CashPOSDB.ProgramFile where ID = '1'";

            MySqlCommand cmd = new MySqlCommand(u, myConnection);
            MySqlDataReader myData;
            try
            {
                Console.WriteLine("connecting to server...");

                Connect();
                Console.WriteLine("connected to server...");
                Console.WriteLine("executing ...");
                cmd.CommandTimeout = 100;

                myData = cmd.ExecuteReader();
                Console.WriteLine("...");
                if (!myData.HasRows)
                    throw new Exception("There are no BLOBs to save");

                myData.Read();
                uint FileSize = myData.GetUInt32(myData.GetOrdinal("FileSize"));
                rawData = new byte[FileSize];

                myData.GetBytes(myData.GetOrdinal("ZipFile"), 0, rawData, 0, (int)FileSize);

                fs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write);
                fs.Write(rawData, 0, (int)FileSize);
                fs.Close();
                Console.WriteLine("download successful...");
                myData.Close();
                Dc();
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                MessageBox.Show("Error " + ex.Number + " has occurred: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.Read();
            }
            return fileName;
        }
        public static void DeleteDirectory(string target_dir)
        {
            Console.WriteLine("deleting zip file...");
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                Console.WriteLine("...");
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
            Console.WriteLine("delete zip file successful...");
        }
        static void unZip(string filePath)
        {
            Console.WriteLine("unzipping...");
            string extractPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\Release\";
            //   string zipPath = extractPath + @"\Release.zip";
            //   using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                //     foreach (ZipArchiveEntry entry in archive.Entries)
                //     {
                //          if (!entry.FullName.Contains("."))
                //         {
                string folder = extractPath;
                if (Directory.Exists(folder))
                    DeleteDirectory(folder);
                //      }
                //   Console.WriteLine(entry.FullName);
                //       //entry.ExtractToFile(Path.Combine(destFolder, entry.FullName));
                //    }
            }
            ZipFile.ExtractToDirectory(filePath, extractPath);
            unZipedFolder = filePath;
            Console.WriteLine("unzip completed...");

        }
        static void deleteZip(string file)
        {
            File.Delete(file);
        }
    }
}
