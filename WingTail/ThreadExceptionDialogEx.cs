#region License statement
/* WingTail is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 of the License.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace WingTail
{
    public partial class ThreadExceptionDialogEx : Form
    {
        public CrashReportDetails CrashReport { get; set; }

        public Action<CrashReportDetails> SendReportEvent;

        public ThreadExceptionDialogEx(Exception exception)
        {
            InitializeComponent();

            Text = Application.ProductName + " - Error Report";

            ShowInTaskbar = Application.OpenForms.Count == 0;

            _pictureBox.Image = SystemIcons.Error.ToBitmap();

            _reportText.Text = "Unhandled exception has occurred in the application:";
            _reportText.Text += Environment.NewLine;
            _reportText.Text += Environment.NewLine + exception.Message;
            _reportText.Text += Environment.NewLine;
            _reportText.Text += Environment.NewLine + "Please press 'Send Report' to notify " + Application.CompanyName;

            CrashReport = new CrashReportDetails();
            CrashReport.Items.Add(new ExceptionReport(exception));
            CrashReport.Items.Add(new ApplicationReport());
            CrashReport.Items.Add(new SystemReport());
            CrashReport.Items.Add(new MemoryPerformanceReport());
        }

        private void ThreadExceptionDialogEx_Load(object sender, EventArgs e)
        {
            foreach (object reportItem in CrashReport.Items)
            {
                _reportListBox.Items.Add(reportItem);
            }
        }

        private void _detailsBtn_Click(object sender, EventArgs e)
        {
            _reportListBox.Visible = !_reportListBox.Visible;
            if (_reportListBox.Visible)
                this.Height += 150;
            else
                this.Height -= 150;
        }

        private void _abortBtn_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void _sendReportBtn_Click(object sender, EventArgs e)
        {
            try
            {
                using (new HourGlass(this))
                {
                    Action<CrashReportDetails> handler = SendReportEvent;
                    if (handler != null)
                        handler(CrashReport);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to send report");
            }
            DialogResult = DialogResult.Ignore;
            Close();
        }

        private void ThreadExceptionDialogEx_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.Control | Keys.C))
            {
                Clipboard.SetText(_reportText.Text);
            }
        }

        private void _reportListBox_DoubleClick(object sender, EventArgs e)
        {
            object reportItem = _reportListBox.SelectedItem;
            if (reportItem != null)
            {
                using (System.IO.StringWriter stringWriter = new System.IO.StringWriter())
                {
                    //Create our own namespaces for the output
                    System.Xml.Serialization.XmlSerializerNamespaces ns = new System.Xml.Serialization.XmlSerializerNamespaces();
                    ns.Add("", "");
                    System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(reportItem.GetType());
                    x.Serialize(stringWriter, reportItem, ns);
                    MessageBox.Show(stringWriter.ToString(), reportItem.GetType().ToString());
                }
            }
        }

        private void _reportListBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyData == Keys.Return)
                e.IsInputKey = true;    // Steal the key-event from parent from
        }

        private void _reportListBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                _reportListBox_DoubleClick(sender, e);
                e.Handled = true;
            }
        }
    }

    public class CrashReportDetails
    {
        [System.Xml.Serialization.XmlArray("ReportItems")]
        [System.Xml.Serialization.XmlArrayItem("Item")]
        public List<object> Items { get; set; }

        public CrashReportDetails()
        {
            Items = new List<object>();
        }

        public Type[] GetItemTypes()
        {
            List<Type> typesList = new List<Type>();
            foreach (object reportItem in Items)
                typesList.Add(reportItem.GetType());
            return typesList.ToArray();
        }
    }

    public class ExceptionReport
    {
        public string ExceptionDetails { get; set; }
        public string StackTrace { get; set; }
        public string ExceptionSource { get; set; }

        public ExceptionReport()
        {
        }

        public ExceptionReport(Exception exception)
        {
            StringBuilder exceptionReport = new StringBuilder();
            Exception innerException = exception;
            while (innerException != null)
            {
                exceptionReport.Append("  ");
                exceptionReport.Append(innerException.Message);
                exceptionReport.Append(" (");
                exceptionReport.Append(innerException.GetType().ToString());
                exceptionReport.AppendLine(")");
                innerException = innerException.InnerException;
            }
            ExceptionDetails = exceptionReport.ToString();

            StackTrace = exception.StackTrace;

            ExceptionSource = exception.Source;
        }
    }

    public class ApplicationReport
    {
        public string ApplicationTitle { get; set; }
        public string ApplicationVersion { get; set; }
        public string ProductName { get; set; }
        public string CompanyName { get; set; }

        public ApplicationReport()
        {
            ApplicationTitle = GetAssemblyTitle();
            ApplicationVersion = Application.ProductVersion;
            ProductName = Application.ProductName;
            CompanyName = Application.CompanyName;
        }

        static string GetAssemblyTitle()
        {
            object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
            if (attributes.Length > 0)
            {
                AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                if (titleAttribute.Title != "")
                {
                    return titleAttribute.Title;
                }
            }
            return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
        }
    }

    public class SystemReport
    {
        public string OperatingSystem { get; set; }
        public string Platform { get; set; }
        public string FrameworkVersion { get; set; }
        public string Language { get; set; }

        public SystemReport()
        {
            OperatingSystem os = Environment.OSVersion;
            OperatingSystem = os.VersionString;
            if (IntPtr.Size == 4)
                Platform = "x86";
            else
                Platform = "x64";
            FrameworkVersion = System.Environment.Version.ToString();
            Language = Application.CurrentCulture.EnglishName;
        }
    }

    public class MemoryPerformanceReport
    {
        public long PrivateMemorySize;
        public long VirtualMemorySize;
        public long WorkingSet;
        public long PagedMemorySize;
        public long PeakWorkingSet;
        public long PeakVirtualMemorySize;
        public long PeakPagedMemorySize;

        public MemoryPerformanceReport()
        {
            try
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    PrivateMemorySize = process.PrivateMemorySize64;
                    VirtualMemorySize = process.VirtualMemorySize64;
                    WorkingSet = process.WorkingSet64;
                    PagedMemorySize = process.PagedMemorySize64;
                    PeakWorkingSet = process.PeakWorkingSet64;
                    PeakVirtualMemorySize = process.PeakVirtualMemorySize64;
                    PeakPagedMemorySize = process.PeakPagedMemorySize64;
                }
            }
            catch
            {
            }
        }
    }

}
