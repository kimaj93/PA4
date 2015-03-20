using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Table;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        // Initializes the queue that is used for starting and stopping the web crawler.
        public CloudQueue initializeStartAndStopQ()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient qClient = storageAccount.CreateCloudQueueClient();
            CloudQueue q = qClient.GetQueueReference("startandstop");
            q.CreateIfNotExists();
            return q;
        }

        // Initializes the queue that is used for storing urls!
        public CloudQueue initializeURLQ()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient qClient = storageAccount.CreateCloudQueueClient();
            CloudQueue q = qClient.GetQueueReference("urls");
            q.CreateIfNotExists();
            return q;
        }

        // Initializes the table that maps words to URLs. 
        public CloudTable initializeTable()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("urltable");
            table.CreateIfNotExists();
            return table;
        }

        // Encodes a URL and returns it.
        private String EncodeURL(String url)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(url);
            String encodedString = System.Convert.ToBase64String(bytes);
            return encodedString.Replace('/', '_');
        }

        // Initializes the queue that is used for storing robot files!
        public CloudQueue initializeRobotsQ()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient qClient = storageAccount.CreateCloudQueueClient();
            CloudQueue q = qClient.GetQueueReference("robots");
            q.CreateIfNotExists();
            return q;
        }

        // Worker role that parses the robots.txt and adds information to Azure table.
        public override void Run()
        {
            // Worker role should keep checking to see what it should do.
            while (true)
            {
                // Slight pause for testing and resting the machine!
                Thread.Sleep(1000);
                // Initialize the startAndStopQ to see if web crawling should start.
                CloudQueue startAndStopQ = initializeStartAndStopQ();
                // Get the message from the startAndStopQ.
                CloudQueueMessage startAndStopQMessage = startAndStopQ.PeekMessage();
                if (startAndStopQMessage != null)
                {
                    // Get the robots.txt's from urlQ
                    CloudQueue robotsQ = initializeRobotsQ();
                    // Get the only message in it so far!
                    CloudQueueMessage robotsMessage = robotsQ.PeekMessage();
                    if (robotsMessage != null)
                    {
                        // Save robots.txt's in a string
                        String robotsString = robotsMessage.AsString;
                        // Initialize URLQ.
                        CloudQueue URLQ = initializeURLQ();
                        // Array for splitting rules.
                        Char[] splitByNewLine = { '\n' };
                        // List for robots.txt's string that is split by new lines.
                        List<String> robotsLines = robotsString.Split(splitByNewLine).ToList();
                        // List for direct sitemap urls for CNN & Bleacher Report
                        List<String> siteMapURLs = new List<String>();
                        // List for CNN disallows
                        List<String> CNNDisallows = new List<String>();
                        // List for Bleacher Report disallows
                        List<String> BleacherReportDisallows = new List<String>();
                        // "Sitemap:" will be checked for over and over.
                        String siteMapWord = "Sitemap:";
                        // "Disallow:" will be checked for over and over
                        String disallowWord = "Disallow:";
                        // start index is needed over and over for sitemaps and disallows
                        int startIndex = 0;
                        // String for URL 
                        String URL = "";
                        // String for disallow directory
                        String disallow = "";
                        // Boolean for which robot txt we are in, Bleacher Report is parsed first
                        Boolean BleacherReportRobots = true;
                        // Loop through each line, and add all each siteMapURL to siteMapURLs list.
                        // Add CNN & Bleacher Report disallows to their respective lists. 
                        for (int i = 0; i < robotsLines.Count; i++) 
                        {
                            String robotLine = robotsLines[i];
                            // Check to see if line has a sitemap url.
                            if (robotLine.StartsWith(siteMapWord))
                            {
                                // start index is the length of the "sitemap:" word
                                startIndex = siteMapWord.Length;
                                // string of sitemap url and trim for white space!
                                URL = robotLine.Substring(startIndex).Trim();
                                // check to see if we are in the cnn robots or bleacher report robots
                                if (URL.StartsWith("http://www.cnn"))
                                {
                                    // no longer in cnn robots.txt, we are in bleacher report robots.txt!
                                    BleacherReportRobots = false;
                                }
                                // Add to siteMapURLsWithAnotherLevel list regardless of whether it is CNN or Bleacher Report.
                                if (BleacherReportRobots && URL.EndsWith("nba.xml") || !BleacherReportRobots)
                                {
                                    siteMapURLs.Add(URL);
                                }
                                // Check for disallow rules!
                            }
                            else if (robotLine.StartsWith(disallowWord))
                            {
                                // start index is the length of the "disallow:" word
                                startIndex = disallowWord.Length;
                                // string of disallow directory and trim for white space!
                                disallow = robotLine.Substring(startIndex).Trim();
                                // Add to Bleacher Report disallows
                                if (BleacherReportRobots)
                                {
                                    BleacherReportDisallows.Add(disallow);
                                }
                                // Add to CNN disallows
                                else
                                {
                                    CNNDisallows.Add(disallow);
                                }
                            }
                        }
                        // Need to find where the location tag is to get the url.
                        String locationTag = "<loc>";
                        // Need to find where the closing location tag is to get the url.
                        String closingLocationTag = "</loc>";
                        // End index for using substring to get the url.
                        int endIndex = 0;
                        // Contents of sitemap needs to be saved in a string.
                        String contentsOfSiteMap = "";
                        // Contents of sitemap lines needs to be saved in a list.
                        List<String> contentsOfSiteMapList = new List<String>();   
                        // Tags for dates in xmls.
                        String newsPubOpenTag = "<news:publication_date>";
                        String newsPubClosingTag = "</news:publication_date>";
                        String lastModOpenTag = "<lastmod>";
                        String lastModClosingTag = "</lastmod>";
                        // Keywords in html that show dates. 
                        // publish_date: "2014/01/21"
                        String pubDateWordWithSpace = "publish_date: ";
                        // "publishDate":"2015-03-06T21:06:59Z"
                        String pubDateWordWithQuotes = "\"publishDate\":";
                        int dateStartIndex = 0;
                        int dateEndIndex = 0;
                        String dateOfSiteMapOrURL = "";
                        // Grab URLs from SiteMaps!
                        for (int i = 0; i < siteMapURLs.Count; i++)
                        {
                            // Save content of Sitemap as a string
                            contentsOfSiteMap = new WebClient().DownloadString(siteMapURLs[i]);
                            // Split it by lines
                            contentsOfSiteMapList = contentsOfSiteMap.Split(splitByNewLine).ToList();
                            // Loop through each line.
                            for (int j = 0; j < contentsOfSiteMapList.Count; j++)
                            {
                                String XMLLine = contentsOfSiteMapList[j];
                                // Start index for substring is after the location tag
                                startIndex = XMLLine.IndexOf(locationTag) + locationTag.Length;
                                // End index for substring is before the closing location tag
                                endIndex = XMLLine.IndexOf(closingLocationTag);
                                // siteMap URL is between the start and end indexes.
                                // Check if the sitemap or url is too old. 
                                Boolean tooOld = false;
                                // Checking for two types of date tags. lastMod & newsPub
                                // First check if lastMod tag exists.
                                dateStartIndex = XMLLine.IndexOf(lastModOpenTag) + lastModOpenTag.Length;
                                dateEndIndex = XMLLine.IndexOf(lastModClosingTag);
                                // Check if newsPub tag exists.
                                if (dateStartIndex == -1 && dateEndIndex == -1)
                                {
                                    dateStartIndex = XMLLine.IndexOf(newsPubOpenTag) + newsPubOpenTag.Length;
                                    dateEndIndex = XMLLine.IndexOf(newsPubClosingTag);
                                }
                                // There are date tags in the sitemaps!
                                if (dateStartIndex != -1 && dateEndIndex != -1)
                                {
                                    // Substring is start index + how every many indexes you need.
                                    dateOfSiteMapOrURL = XMLLine.Substring(dateStartIndex, dateEndIndex - dateStartIndex);
                                    DateTime d = Convert.ToDateTime(dateOfSiteMapOrURL);
                                    // Date is older than 10 months.
                                    if (DateTime.Now.AddMonths(-10) > d)
                                    {
                                        tooOld = true;
                                    }
                                }
                                // If location tag with url or sitemap xml is found 
                                // And is less than 10 months old!
                                if (startIndex != -1 && endIndex != -1 && !tooOld)
                                {
                                    Boolean okayToCrawl = true;
                                    // Substring is start index + how every many indexes you need.
                                    URL = XMLLine.Substring(startIndex, endIndex - startIndex);
                                    if (URL.StartsWith("http://www.cnn"))
                                    {
                                        foreach (String CNNDisallow in CNNDisallows)
                                        {
                                            if (URL.Contains(CNNDisallow))
                                            {
                                                okayToCrawl = false;
                                                break;
                                            }
                                        }
                                    }
                                    else if (URL.StartsWith("http://bleacherreport"))
                                    {
                                        foreach (String BleacherReportDisallow in BleacherReportDisallows)
                                        {
                                            if (URL.Contains(BleacherReportDisallow))
                                            {
                                                okayToCrawl = false;
                                                break;
                                            }
                                        }
                                    }
                                    // If URL is to a list of more siteMapURLs, add it back to the list.
                                    if (URL.EndsWith(".xml"))
                                    {
                                        // Add the new embedded sitemap to the sitemapurls list
                                        siteMapURLs.Add(URL);
                                        okayToCrawl = false;
                                    }
                                    // Add to URLQ only if it is okay to crawl meaning it doesn't contain a disallow directory,
                                    // and if crawler has not been stopped.
                                    if (okayToCrawl && startAndStopQ.PeekMessage() != null)
                                    {
                                        URLQ.AddMessage(new CloudQueueMessage(URL));
                                        String contentsOfHTML = "";
                                        // Need to find title tags!
                                        String titleTag = "<title>";
                                        String closingTitleTag = "</title>";
                                        List<String> contentsOfHTMLList = new List<String>();
                                        String title = "";
                                        // Loop while there are messages to look at. (URLs)
                                        while (true)
                                        {
                                            // Grab the message from URLQ.
                                            CloudQueueMessage cm = URLQ.GetMessage();
                                            if (cm == null)
                                            {
                                                break;
                                            }
                                            // Delete the message in order to iterate through the queue.
                                            URLQ.DeleteMessage(cm);
                                            // Set url string as the url message.
                                            URL = cm.AsString;
                                            // YOU NEED TO CHECK IF URL IS VALID!!!! BEFORE DOWNLOADING STRING.
                                            // hence the try/catch clause.
                                            try
                                            {
                                                contentsOfHTML = new WebClient().DownloadString(URL);
                                                // Split it by lines
                                                contentsOfHTMLList = contentsOfHTML.Split(splitByNewLine).ToList();
                                                // Get the title!
                                                foreach (String HTMLLine in contentsOfHTMLList)
                                                {
                                                    // Boolean for which type of publish date string is found.
                                                    Boolean pubDateWordWithSpaceB = false;
                                                    String dateOfURL = "";
                                                    // Try looking for publish_date: "2014/01/21"
                                                    dateStartIndex = HTMLLine.IndexOf(pubDateWordWithSpace) + pubDateWordWithSpace.Length;
                                                    // Try looking for "publishDate":"2015-03-06T21:06:59Z"
                                                    if (dateStartIndex == -1)
                                                    {
                                                        dateStartIndex = HTMLLine.IndexOf(pubDateWordWithQuotes);
                                                    } else {
                                                        pubDateWordWithSpaceB = true;
                                                    }
                                                    // Date string has been found
                                                    if (dateStartIndex != -1)
                                                    {
                                                        String dateInURL = "";
                                                        String dt = "";
                                                        // i.e. publish_date: "2014/01/21" was found.
                                                        if (pubDateWordWithSpaceB) {
                                                            dateInURL = HTMLLine.Substring(dateStartIndex, "\"2014/01/21\"".Length);
                                                        // i.e. "publishDate":"2015-03-06T21:06:59Z" was found.
                                                        } else {
                                                            dateInURL = HTMLLine.Substring(dateStartIndex, "\"2015-03-06T21:06:59Z\"".Length);
                                                        }
                                                        DateTime dInURL = Convert.ToDateTime(dateInURL);
                                                        // Set tooOld to true if date is more than 10 months old,
                                                        // and break out of loop because we don't want the title either!
                                                        // It shouldn't be added to Azure Table!
                                                        // Date is older than 10 months.
                                                        if (DateTime.Now.AddMonths(-10) > dInURL)
                                                        {
                                                            tooOld = true;
                                                            break;
                                                        }
                                                    }
                                                    int titleStartIndex = HTMLLine.IndexOf(titleTag);
                                                    int titleEndIndex = HTMLLine.IndexOf(closingTitleTag);
                                                    int substringStartIndex = titleStartIndex + titleTag.Length;
                                                    if (titleStartIndex != -1 && titleEndIndex != -1)
                                                    {
                                                        title = HTMLLine.Substring(substringStartIndex, titleEndIndex - substringStartIndex);
                                                    }
                                                    // Title and date have been found.
                                                    if (!title.Equals("") && !dateOfURL.Equals("")) {
                                                        break;
                                                    }
                                                }
                                                // Only if a title is found and it's not more than 10 months old.
                                                if (!title.Equals("") && !tooOld)
                                                {
                                                    // Initialize table.
                                                    CloudTable titleToURLAndDateTable = initializeTable();
                                                    // MAKE SURE TO REMEMBER THAT TITLES CAN PIPE CHARACTERS LIKE '|', ',''s, etc.
                                                    Char[] splitByWhiteSpace = { ' ', ',', '|', '\'', '\"', '?', '\\', '-' };
                                                    List<String> titleWordsList = title.Split(splitByWhiteSpace).ToList();
                                                    foreach (String titleWord in titleWordsList)
                                                    {
                                                        // Create Entity 
                                                        // Example: Hey money boy, 
                                                        // Entity right now only inserts "Hey" as Partition key at first.
                                                        // The same partition key can have many different urls. 
                                                        Entity e = new Entity(EncodeURL(titleWord.ToLower()), EncodeURL(URL));
                                                        TableOperation insert = TableOperation.InsertOrReplace(e);
                                                        titleToURLAndDateTable.Execute(insert);
                                                    }
                                                }
                                                // Set title back to "" because it could have been adjusted.
                                                title = "";
                                            }
                                            catch 
                                            {
                                                // Initialize table.
                                                CloudTable titleToURLAndDateTable = initializeTable();
                                                Entity e = new Entity("ThisLinkIsBroken", EncodeURL(URL));
                                                TableOperation insert = TableOperation.InsertOrReplace(e);
                                                titleToURLAndDateTable.Execute(insert);
                                            }    
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("WorkerRole1 has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole1 has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}
