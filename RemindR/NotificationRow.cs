namespace RemindR
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
using NetOffice.OutlookApi;

    class NotificationRow
    {
        static Regex rxDate = new Regex(@"\d{2}-\d{2}-\d{4}");
        static Regex rxTime = new Regex(@"\d{1,2}:\d{2}");
        
        internal static NotificationRow Create(string[] evidence)
        {
            var res = default(NotificationRow);
            var dateLine = evidence.FirstOrDefault(s => rxDate.IsMatch(s));
            if (dateLine != null)
            {
                var m = rxDate.Match(dateLine);
                var d = DateTime.ParseExact(m.Value, "dd-MM-yyyy", CultureInfo.InvariantCulture);
                var remainder = dateLine.Substring(m.Index + m.Length);
                var mt = rxTime.Match(remainder);
                if (mt != null && mt.Success)
                {
                    d += TimeSpan.Parse(mt.Value);
                }
                // which lines may look like names?
                var remainingLines = evidence.Except(new[] { dateLine, null, string.Empty }).Where(s => s.Length > 5 && s.Contains(' '));
                res = new NotificationRow
                {
                    DateTime = d,
                    ExtraData = remainingLines.ToArray(),
                };
                res.Resolve();
            }
            return res;
        }

        private Task ResolveAsync()
        {
            return Task.Run(() =>
            {
                Resolve();
            });
        }

        public static NetOffice.OutlookApi.Application app = new NetOffice.OutlookApi.Application();

        private void Resolve()
        {
            // try to resolve
            if (this.ExtraData.Length > 0)
            {
                var mail = (MailItem)app.CreateItem(NetOffice.OutlookApi.Enums.OlItemType.olMailItem);
                var resolved = this.ExtraData
                    .Select(GetWords)
                    .Select(edLine =>
                        {
                            var rcpt = mail.Recipients.Add(string.Join(" ", edLine.Take(2)));
                            var res = rcpt.Resolve() ? rcpt.AddressEntry : null;
                            return res;
                        }).ToArray();
                this.Entries = resolved;
                mail.Delete();
            }
        }

        private bool IsMatch(NetOffice.OutlookApi.AddressEntry ae, string[] wordsIntended)
        {
            var wordsInCandidate = GetWords(ae.Name);
            return (wordsInCandidate.Where(w => wordsIntended.Contains(w)).Count() >= 2);
        }

        private static string[] GetWords(string ed)
        {
            var wordsIntended = ed.Replace(",", " ").ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return wordsIntended;
        }

        public DateTime DateTime { get; set; }

        public string[] ExtraData { get; set; }

        public AddressEntry[] Entries { get; set; }

        public string NotifiedRecipients
        {
            get { return this.Entries == null ? null : string.Join(", ", this.Entries.Select(n => n == null ? "(not found)" : n.Name)); }
        }
    }
}
