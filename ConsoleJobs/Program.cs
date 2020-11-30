﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using Sterling.MSSQL;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System.Security.Cryptography;

namespace ConsoleJobs
{
    class Program
    {
        static void Main(string[] args)
        {
            int val = Convert.ToInt16(ConfigurationManager.AppSettings["diff"]);
            DateTime dt = DateTime.Now.AddDays(val);
            new ErrorLog("Started at " + dt.ToString("yyyy-MM-dd hh:mm:ss:fff") + " !!!");

            

            string[] masterCardUploadFiles = GetMastercardUploadData(dt.AddDays(0));
            if (masterCardUploadFiles == null)
            {
                return;
            }
            int len = masterCardUploadFiles.Length;
            //string[] visaUploadFiles = GetVisaUploadData(dt.AddDays(-1),len);

            //string[] uploadFiles = masterCardUploadFiles.Concat(visaUploadFiles).Distinct().ToArray();
            string[] uploadFiles = masterCardUploadFiles.Distinct().ToArray();
            string ext = ConfigurationManager.AppSettings["ext"];
            
            foreach (string sFile in uploadFiles)
            {
                string hr = DateTime.Now.Hour.ToString();
                string outputFile = ConfigurationManager.AppSettings["UploadPath"] + "\\" + sFile + "." + ext;
                if (EncryptFile(sFile, ext, outputFile))
                {
                    if (SftpUpload(outputFile))
                    {
                        //send to mail list
                        string[] sendmails = ConfigurationManager.AppSettings["recMail"].Split(';');

                        //copy mail list
                        string[] copymails = ConfigurationManager.AppSettings["cMail"].Split(';');

                        string sender = ConfigurationManager.AppSettings["fMail"];

                        //string filedt = dt.AddDays(-1).ToString("dddd, MMMM dd, yyyy");

                        string msgBody = $"<font>Dear Cards and Switching Team,<br>Securecode file {sFile} for {dt.ToString("dddd, MMMM dd, yyyy")}, {dt.Hour.ToString()}_hour has been Successfully Uploaded!!!</br></font>";
                        msgBody += "<br></br><b>NB: THIS IS AN AUTOGENERATED MAIL. PLEASE DO NOT RESPOND!!!</b>";
                        msgBody += "</br><br><b>For any Clarifications, Please contact the <a href=\"mailto:csu@Sterlingbankng.com\">Card and Switching Services Unit</a> </b></br>";
                        new ErrorLog("Sending Email......");
                        SendMail(sender, msgBody, "SecureCode File Upload", sendmails, copymails, sFile);
                        new ErrorLog("Success");
                    }
                    else
                    {
                        new ErrorLog("Error Uploading File "+ sFile + "." + ext);
                    }
                }
                else
                {
                    new ErrorLog("Error Encrypting file "+sFile);
                }
                //string fromPath = ConfigurationManager.AppSettings["GenPath"];
                //string toPath = ConfigurationManager.AppSettings["ArcPath"];
                //Directory.Move(fromPath, toPath);
            }
        }

        //Retrieve the data to be uploaded
        public static string[] GetMastercardUploadData(DateTime dt)
        {
            VoguePayService _vpService = new VoguePayService();
            DateTime vardate2 = DateTime.Now;
            int hourDiff = Convert.ToInt32(ConfigurationManager.AppSettings["hourDiff"].ToString());
            //double minDiff = Convert.ToInt32(ConfigurationManager.AppSettings["minDiff"].ToString());
            //string getDate = DateTime.Now.ToString("yyyy-MM-dd");
            //int endHour = DateTime.Now.Hour - 1;
            //int endHour = DateTime.Now.Hour;
            //int curHour = DateTime.Now.Hour;
            //int startHour = curHour - hourDiff;
            string startTime= DateTime.Now.AddHours(hourDiff).ToString("yyyy-MM-dd HH:mm:ss.fff");
            string endTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            //int hr = DateTime.Now.Hour;
            //int hr2 = hr - 1;
            //string _hr = hr.ToString();
            //string _hr2 = hr.ToString();
            DataSet ds = new DataSet();
            string[] filenames = null; int j = 1;
            //string product = ConfigurationManager.AppSettings["product"];
            
            string stat = ConfigurationManager.AppSettings["status"];
            string pth = ConfigurationManager.AppSettings["GenPath"];
            //string dte = dt.ToString("yyyy-MM-dd");
            string dte = dt.Year.ToString() + "-" + dt.Month.ToString() + "-" + dt.Day.ToString();

            string productName = ConfigurationManager.AppSettings["Product_Name"];
            string _names = string.Empty;
            if (productName.Contains("|"))
            {
                string[] _prodNames = productName.Split('|');
                foreach (string item in _prodNames)
                {
                    _names += "'" + item + "',";
                }
                _names = _names.Remove(_names.Length - 1, 1);
            }
            else
            {
                _names = "'" + productName + "'";
            }

            string sql2 = $"select a.pan as PAN,b.customer_id as CUSTOMERNUMBER,'' as CELLPHONE,'' as EMAIL,'' as STATUSFLAG, account_id as ACCOUNT from pc_cards_1_a a with(nolock) inner join pc_customers_1_A b with (nolock) on a.customer_id = b.customer_id join pc_card_accounts_1_A c on a.pan = c.pan where card_program in ({_names}) and card_status = 1 and date_issued between '{startTime}' AND '{endTime}'";

            Connect cn = new Connect("FEPConn")
            {
                Persist = true
            };
            cn.SetSQL(sql2);
            ds = cn.Select();
            cn.CloseAll();

            bool hasRows = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasRows)
            {
                string hr = DateTime.Now.Hour.ToString();
                string min = DateTime.Now.Minute.ToString();
                int rows = ds.Tables[0].Rows.Count;
                if (rows > 0)
                {
                    string[] sc = new string[5];
                    string filename = "SBP" + dt.ToString("ddMMyy") + "V" + hr + min;
                    new ErrorLog(rows + " records records to process!!!");
                    int len = rows / 5000;
                    int rem = rows % 5000;
                    if (rem > 0)
                    {
                        len++;
                    }

                    filenames = new string[len];
                     int k = 1;
                    filenames[0] = filename + j+".csv";
                    for (int i=0;i<rows;i++)
                    {
                        try
                        {

                            new ErrorLog("Processing record " + i);
                           
                            DataRow dr = ds.Tables[0].Rows[i];
                            //string pan = dr[0].ToString();
                            string pan = dr[0].ToString();
                            //string pan = IBS_Decrypt(encpan);

                            string account = dr[5].ToString();
                            var custDetails = _vpService.Rest_Get(account);
                            //custDetails.status = "success";
                            if (custDetails.code == "OK")
                            {
                                sc[0] = "ADD";
                                sc[1] = pan.Trim();
                                sc[2] = "";
                                sc[3] = "";
                                sc[4] = "1";
                                //sc[5] = "MobilePhone";
                                //sc[6] = "NULL";
                                string cusnum = dr[1].ToString().Remove(0,4);
                                string email = custDetails.data.emailAddress;//(rd["ContactEmail"].ToString().Replace('|', ' ')).Trim();
                                string mobile = custDetails.data.phoneNumber; //(rd["ContactMobile1"].ToString()).Trim();
                                //string email = "test@sterling.ng";//(rd["ContactEmail"].ToString().Replace('|', ' ')).Trim();
                                //string mobile = "08066667777";
                                if (mobile.Length == 11) { mobile = "+234" + mobile.Remove(0, 1); }
                                //mobile = mobile.Length == 11 ? mobile = "+234" + mobile.Remove(0, 1) : mobile;

                                if ((email == "info@gmail.com") || (email == "na@yahoo.com") || (email == "none@gmail.com") || (email == "info@sterlingbankng.com") || (email == "none@yahoo.com") || (email == "none@sterlingbankng.com") || (email == "info@yahoo.com") || (email == "info@gmail.com") || (email == "customer@info.com") || (email == "email@customer.com"))
                                {
                                    email = "";
                                }
                                else if ((email == "info@gmail.com".ToUpper()) || (email == "na@yahoo.com".ToUpper()) || (email == "none@gmail.com".ToUpper()) || (email == "info@sterlingbankng.com".ToUpper()) || (email == "none@yahoo.com".ToUpper()) || (email == "none@sterlingbankng.com".ToUpper()) || (email == "info@yahoo.com".ToUpper()) || (email == "info@gmail.com".ToUpper()) || (email == "customer@info.com".ToUpper()) || (email == "email@customer.com".ToUpper()))
                                {
                                    email = "";
                                }
                                if ((!string.IsNullOrEmpty(email)) && (!string.IsNullOrEmpty(mobile)))
                                {
                                    if (mobile == "N/A")
                                    {
                                        mobile = "";
                                    }
                                    sc[3] = email.Trim();
                                    sc[2] = mobile.Trim();
                                    GenGenericDelimCsv(pth, filename + j, sc, ",");

                                    new ErrorLog("Processed record " + k + " in batch" + filename + j);
                                    if (k >= 5000)
                                    {
                                        k = 1;
                                        filenames[j] = filename + (j + 1) + ".csv";
                                        j++;
                                    }
                                    else
                                    {
                                        k++;
                                    }
                                }
                                else if (!string.IsNullOrEmpty(email))
                                {
                                    sc[3] = email.Trim();
                                    GenGenericDelimCsv(pth, filename + j, sc, ",");
                                    new ErrorLog("Processed record " + k + " in batch" + filename + j);
                                    if (k >= 5000)
                                    {
                                        k = 1;
                                        filenames[j] = filename + (j + 1) + ".csv";
                                        j++;
                                    }
                                    else
                                    {
                                        k++;
                                    }
                                }
                                else if (!string.IsNullOrEmpty(mobile))
                                {
                                    if (mobile == "N/A")
                                    {
                                        mobile = "";
                                    }
                                    sc[2] = mobile.Trim();
                                    GenGenericDelimCsv(pth, filename + j, sc, ",");
                                    new ErrorLog("Processed record " + k + " in batch" + filename + j);
                                    if (k >= 5000)
                                    {
                                        k = 1;
                                        filenames[j] = filename + (j + 1) + ".csv";
                                        j++;
                                    }
                                    else
                                    {
                                        k++;
                                    }
                                }
                                else
                                {
                                    sc[3] = "";
                                    sc[2] = "";
                                    GenGenericDelimCsv(pth, filename + "ErrorFile", sc, ",");
                                }
                            }
                            else { continue; }
                            //stop here
                        }
                        catch (Exception ex)
                        {
                            new ErrorLog(ex);
                        }
                    }
                }
            }

            return filenames;
        }

        public static void GenGenericDelimCsv(string folderpth, string filename, string[] data, string delim)
        {
            Thread.Sleep(10);
            string pth = folderpth + "\\" + filename + ".csv";

            if (data.Length > 0)
            {
                string txt = data[0];
                for (int i = 1; i < data.Length; i++)
                {
                    txt += delim + data[i];
                }
                if (!File.Exists(pth))
                {
                    using (StreamWriter sw = File.CreateText(pth))
                    {
                        sw.WriteLine("action,pan,mobilenumber,email,segmentationindicator");
                        sw.WriteLine(txt);
                        sw.Close();
                        sw.Dispose();
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(pth))
                    {
                        sw.WriteLine(txt);
                        sw.Close();
                        sw.Dispose();
                    }
                }
            }
            else
            {
                new ErrorLog("No record to write to file!!!");
            }
        }

        public static bool EncryptFile(string filename,string ext,string outputFile)
        {
            bool stat = false;
            new ErrorLog("File to Encryppt:- " + filename+". \n Getting necceccary parameters for encryption!!!"); 

            string ky = ConfigurationManager.AppSettings["keyId"];
            string workingDirectory = ConfigurationManager.AppSettings["gpgPath"];
            string cmdPth = ConfigurationManager.AppSettings["cmdPath"]+"cmd.exe";            
            string pth = ConfigurationManager.AppSettings["GenPath"];
            //string outputFile = ConfigurationManager.AppSettings["UploadPath"] + "\\"+filename+"." + ext;
            string file = pth + "\\" + filename;

            ProcessStartInfo psi = new ProcessStartInfo(cmdPth)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            psi.RedirectStandardOutput = true;
            psi.WorkingDirectory = workingDirectory;


            try
            {
                //gpg --recipient 729D33DB27ED9CAF --output F:\Genfiles\SecureCode\Uploads\Today\SBP110517V1.csv.gpg --encrypt --trust-model always F:\Genfiles\SecureCode\Generated\Today\SBP110517V1.csv
                string command = "gpg --recipient " + ky + " --output " + outputFile + " --encrypt --trust-model always " + file;
                new ErrorLog(command + "\n About to encrypt " + filename);
                Process proc = Process.Start(psi);
                proc.StandardInput.WriteLine(command);
                proc.StandardInput.Flush();
                proc.StandardInput.Close();
                proc.WaitForExit();
                proc.Close();

                if (File.Exists(outputFile))
                {
                    new ErrorLog(filename + "Encrypted Successfully!!! \n Pick file " + filename + "." + ext + "in " + ConfigurationManager.AppSettings["UploadPath"]);
                    stat = true;
                }
                else
                {
                    stat = false;
                }
            }
            catch (Exception ex)
            {
                new ErrorLog(ex);
            }
            return stat;
        }

        public static bool SftpUpload(string uploadfile)
        {
            bool stat = false;

            new ErrorLog("About to connect to the SFTP...");
            string sftpHost = ConfigurationManager.AppSettings["sftpHost"];
            int sftpPort = Convert.ToInt32(ConfigurationManager.AppSettings["sftpPort"]);
            string sftpUser = ConfigurationManager.AppSettings["sftpUser"];
            string sftpPwd = ConfigurationManager.AppSettings["sftpPwd"];
            string sftpPath = ConfigurationManager.AppSettings["sftpPth"];

            using (var sClient = new SftpClient(sftpHost, sftpPort, sftpUser, sftpPwd))
            {
                //Connect to the SFTP...
                sClient.Connect();

                if (sClient.IsConnected)
                {
                    new ErrorLog("Connected to the SFTP " + sftpHost + " on port "+sftpPort+" with user "+sftpUser+" ...");
                    new ErrorLog(".............");

                    sClient.ChangeDirectory(sftpPath);
                    new ErrorLog("Directory Changed to " + sftpPath + " ...");

                    new ErrorLog("Uploading File "+ Path.GetFileName(uploadfile) + " to " + sftpPath + " ...");
                    using (var fileStream = new FileStream(uploadfile, FileMode.Open))
                    {
                        new ErrorLog("Uploading File " + Path.GetFileName(uploadfile) + " of size "+ fileStream.Length + " to " + sftpPath + " ...");
                        //new ErrorLog("Uploading {0} ({1:N0} bytes)", uploadfile, fileStream.Length);
                        sClient.BufferSize = 4 * 1024; // bypass Payload error large files
                        sClient.UploadFile(fileStream, Path.GetFileName(uploadfile),null);
                    }

                    //Verify that the file was actually uploaded.
                    string ff = sftpPath + "/" + Path.GetFileName(uploadfile);
                    try
                    {                        
                        SftpFileAttributes fa = sClient.GetAttributes(ff);
                        new ErrorLog("File " + ff + " uploaded Successfully!!!....");
                        stat = true;
                    }
                    catch
                    {
                        new ErrorLog("File " + ff + " not uploaded Successfully!!!....");
                    }
                }
            }

           return stat;
        }

        public static void SendMail(string mailFrom, string body, string sbj, string[] mails, string[] mailc,string attachFile)
        {
            Mailer m = new Mailer();

            m.addFrom(mailFrom);
            //m.mailFrom = mailFrom;

            // recipient address
            try
            {
                if (mails.Length != 0)
                {
                    string[] smail = mails;
                    for (int n = 0; n < smail.Length; n++)
                    {
                        m.addTo(smail[n]);
                    }
                    m.attchFile(attachFile, "");
                }
            }
            catch (Exception ex)
            {
                new ErrorLog(ex);
            }

            try
            {
                if (mailc.Length != 0)
                {
                    string[] cmail = mailc;
                    for (int c = 0; c < cmail.Length; c++)
                    {
                        m.addCC(mailc[c]);
                    }
                }
            }
            catch (Exception ex)
            {
                new ErrorLog(ex);
            }

            // Set the subject of the mail message
            m.mailSubject = sbj;
            // Set the body of the mail message

            m.mailBody = body;
            // Set the format of the mail message body as HTML
            m.msgHtml = true;

            // Instantiate a new instance of SmtpClient
            m.sendTheMail();
        }
        public static string Encrypt(String val)
        {
            var pp = string.Empty;
            MemoryStream ms = new MemoryStream();
            //string rsp = "";
            try
            {
                var sharedkeyval = "000000010000001000000011000001010000011100001011000011010001000100010010000100010000110100001011000001110000001100000100000010000000000100000010000000110000010100000111000010110000110100001101";
                sharedkeyval = BinaryToString(sharedkeyval);
                var sharedvectorval = "0000000100000010000000110000010100000111000010110000110100000011";
                sharedvectorval = BinaryToString(sharedvectorval);
                //sharedvectorval = BinaryToString(sharedvectorval);
                byte[] sharedkey = System.Text.Encoding.GetEncoding("utf-8").GetBytes(sharedkeyval);
                byte[] sharedvector = System.Text.Encoding.GetEncoding("utf-8").GetBytes(sharedvectorval);

                TripleDESCryptoServiceProvider tdes = new TripleDESCryptoServiceProvider();
                byte[] toEncrypt = Encoding.UTF8.GetBytes(val);

                CryptoStream cs = new CryptoStream(ms, tdes.CreateEncryptor(sharedkey, sharedvector), CryptoStreamMode.Write);
                cs.Write(toEncrypt, 0, toEncrypt.Length);
                cs.FlushFinalBlock();
                pp = Convert.ToBase64String(ms.ToArray());
            }
            catch (Exception ex)
            {
                //logger.Error(ex);
                Console.WriteLine("Error encrypting pan " + pp.Substring(0, 6) + " * ".PadLeft(pp.Length - 10, '*') + pp.Substring(pp.Length - 4, 4) + " is " + ex.ToString());
                pp = val;
            }
            return pp;
        }

        public static string IBS_Decrypt(string val)
        {
            var pp = string.Empty;

            try
            {
                var sharedkeyval = "000000010000001000000011000001010000011100001011000011010001000100010010000100010000110100001011000001110000001100000100000010000000000100000010000000110000010100000111000010110000110100001101";
                sharedkeyval = BinaryToString(sharedkeyval);
                var sharedvectorval = "0000000100000010000000110000010100000111000010110000110100000011";
                sharedvectorval = BinaryToString(sharedvectorval);
                byte[] sharedkey = Encoding.GetEncoding("utf-8").GetBytes(sharedkeyval);
                byte[] sharedvector = Encoding.GetEncoding("utf-8").GetBytes(sharedvectorval);
                TripleDESCryptoServiceProvider tdes = new TripleDESCryptoServiceProvider();
                byte[] toDecrypt = Convert.FromBase64String(val);
                MemoryStream ms = new MemoryStream();
                CryptoStream cs = new CryptoStream(ms, tdes.CreateDecryptor(sharedkey, sharedvector), CryptoStreamMode.Write);
                cs.Write(toDecrypt, 0, toDecrypt.Length);
                cs.FlushFinalBlock();
                pp = Encoding.UTF8.GetString(ms.ToArray());
            }
            catch (Exception ex)
            {
                //logger.Error(ex);
                pp = val;
            }
            return pp;
        }

        private static string BinaryToString(string binary)
        {
            if (string.IsNullOrEmpty(binary))
                throw new ArgumentNullException("binary");

            if ((binary.Length % 8) != 0)
                throw new ArgumentException("Binary string invalid (must divide by 8)", "binary");

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < binary.Length; i += 8)
            {
                string section = binary.Substring(i, 8);
                int ascii = 0;
                try
                {
                    ascii = Convert.ToInt32(section, 2);
                }
                catch
                {
                    throw new ArgumentException("Binary string contains invalid section: " + section, "binary");
                }
                builder.Append((char)ascii);
            }
            return builder.ToString();
        }

    }
}
