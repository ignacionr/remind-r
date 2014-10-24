namespace RemindR
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Diagnostics;
    using System.Drawing;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Xml;

    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        static Regex rxAllTRs = new Regex(@"<tr>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        static Regex rxDate = new Regex(@"\d{2}-\d{2}-\d{4}");

        private async void button1_Click(object sender, EventArgs e)
        {
            using (var wc = new WebClient())
            {
                try
                {
                    var text = await wc.DownloadStringTaskAsync(this.textURL.Text);
                    // look for dates within rows
                    var rowsWithDates = rxAllTRs.Matches(text).Cast<Match>().Select(m => m.Value).Where(v => rxDate.IsMatch(v));
                    foreach (var m in rowsWithDates)
                    {
                        var doc = new XmlDocument();
                        doc.LoadXml(m);
                        var evidence = doc.SelectNodes("//td").Cast<XmlElement>().Select(el => el.InnerText).ToArray();
                        this.ReportRow(evidence);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void ReportRow(string[] evidence)
        {
            var row = NotificationRow.Create(evidence);
            if (row != null && row.DateTime > DateTime.Now)
            {
                this.notificationRowBindingSource.Add(row);
            }            
        }

        private void cmdSend_Click(object sender, EventArgs e)
        {
            foreach (var row in this.notificationRowBindingSource.List.Cast<NotificationRow>())
            {
                var appt = (NetOffice.OutlookApi.AppointmentItem)NotificationRow.app.CreateItem(NetOffice.OutlookApi.Enums.OlItemType.olAppointmentItem);
                appt.MeetingStatus = NetOffice.OutlookApi.Enums.OlMeetingStatus.olMeeting;
                appt.Start = row.DateTime;
                appt.End = row.DateTime.AddDays(1);
                appt.Body = string.Format("You have been selected in the following schedule item:\r\n{0}\r\n{1}", row.DateTime, string.Join("\r\n", row.ExtraData));
                row.Entries.ToList().ForEach(entry =>
                {
                    var rcpt = appt.Recipients.Add(entry.Address);
                    rcpt.Type = 1; // required
                    rcpt.Resolve();
                });
                appt.Subject = this.textURL.Text;
                appt.Save();
                appt.Send();
            }
        }

    }
}
