using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;
using MySql.Data.MySqlClient;

namespace WebRole1
{
    /// <summary>
    /// Summary description for WebService1
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class WebService1 : System.Web.Services.WebService
    {

  
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

        // Decodes an encoded URL and returns it as a String.
        private String DecodeURL(String encodedURL)
        {
            String base64String = encodedURL.Replace('_', '/');
            byte[] bytes = System.Convert.FromBase64String(base64String);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        // Returns the size of the URL queue, 
        // which is the number of URLs that have not been crawled yet.
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public int getQSize()
        {
            CloudQueue URLQ = initializeURLQ();
            if (URLQ.PeekMessage() != null)
            {
                // Fetch the queue attributes.
                URLQ.FetchAttributes();
                // Retrieve the cached approximate message count.
                return (int)URLQ.ApproximateMessageCount;
            }
            return 0;
        }

        // Clears all queues and tables for debugging and user interface functionality.
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public void clearEverything()
        {
            CloudQueue startAndStopQ = initializeStartAndStopQ();
            CloudQueue URLQ = initializeURLQ();
            CloudQueue robotsQ = initializeRobotsQ();
            startAndStopQ.Clear();
            URLQ.Clear();
            robotsQ.Clear();
            CloudTable table = initializeTable();
            table.Delete();
        }

       
        // Starts the web crawler on CNN and Bleacher Report robots.txt.
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public String startCrawling()
        {   
            // Put robots txt into robot queue.
            CloudQueue robotsQ = initializeRobotsQ();
            // Contents of CNN robots.txt.
            String contentsOfCNNRobots = new WebClient().DownloadString("http://cnn.com/robots.txt");
            // Contents of Bleacher Report robots.txt.
            String contentsOfBleacherRobots = new WebClient().DownloadString("http://bleacherreport.com/robots.txt");
            // Combine robots.txt's into one string.
            String m = contentsOfBleacherRobots + contentsOfCNNRobots;
            // Add to url queue
            robotsQ.AddMessage(new CloudQueueMessage(m));
            // Start crawling by initializing the startAndStopQ.
            CloudQueue startAndStopQ = initializeStartAndStopQ();
            // startAndStopQ is not null, so worker role should run!
            startAndStopQ.AddMessage(new CloudQueueMessage("Start"));
            // Return the combined robots.txt's for testing to make sure the queue contains the text.
            return "Crawling these robot files: \n" + robotsQ.PeekMessage().AsString;
        }

        // Encodes a URL and returns it.
        private String EncodeURL(String url)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(url);
            String encodedString = System.Convert.ToBase64String(bytes);
            return encodedString.Replace('/', '_');
        }

        public static Dictionary<String, List<String>> URLCacheDictionary = new Dictionary<String, List<String>>();
        // Returns URLs from the URL table whose titles match the given user input. 
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public String GetURLsAndErrors(String userInput)
        {
            // Get table
            CloudTable table = initializeTable();
            // Split input by white space because it will most likely be more than one word.
            Char[] splitBySpaces = { ' ' };
            // Example: input is "Obama says yup"
            // Now list has "Obama", "says", "yup"
            List<String> userInputList = userInput.Split(splitBySpaces).ToList();
            // Initialize rangeQuery.
            TableQuery<Entity> rangeQuery = new TableQuery<Entity>();
            // Only want ten results! this is a counter.
            int j = 0;
            // List for found urls in table
            List<String> URLList = new List<String>();
            // Overall counter
            int count = 0;
            // Loop for each index of the sentence array.
            for (int i = 0; i < userInputList.Count; i++)
            {
                // Grab the query for each word. example: userInputArray[0] = "Obama".
                rangeQuery = new TableQuery<Entity>().Where(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, EncodeURL(userInputList[i].ToLower())));
                // Loop through each entity in "Obama" table for the example above.
                foreach (Entity e in table.ExecuteQuery(rangeQuery))
                {
                    // Increment overall count.
                    count++;
                    // count each entity, only want ten for now.
                    j++;
                    // Grab the url from entity.
                    String url = DecodeURL(e.RowKey);
                    // Add url to the URLList
                    URLList.Add(url);
                    // Get out of this loop once ten urls are found! (per word)
                    if (j == 10)
                    {
                        j = 0;
                        break;
                    }
                }
            }
            // Turn the list into a string with new lines in between each url.
            String allURLs = String.Join("<br>", URLList.ToArray());
            // Return number of urls and all urls.
            return "<br>" + count + " results for " + userInput + ":<br>" + allURLs;
        }

        public Trie<String> trie;

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<String> getSuggestedWords(String lettersTyped)
        {
            if (URLCacheDictionary.ContainsKey(lettersTyped))
            {
                // Return cache in memory if available.
                return URLCacheDictionary[lettersTyped];
            }
            else
            {
                trie = buildTrie();
                String results = "";
                foreach (Char c in lettersTyped)
                {
                    results += trie.sug.addNextLetter(c);
                }
                List<String> suggestions = new List<String>();
                foreach (TrieNode<String> node in trie.terminalNodes())
                {
                    TrieNode<String> n = node;
                    String word = "";
                    while (n != trie.overallRoot)
                    {
                        word += n.Key.ToString();
                        n = n.Mom;
                    }
                    char[] cArray = word.ToCharArray();
                    Array.Reverse(cArray);
                    word = new String(cArray);
                    suggestions.Add(word);
                }
                suggestions.Reverse();
                List<String> sugg = new List<String>();
                if (trie.sug.currentPrefix != null)
                {
                    int i = 0;
                    foreach (String title in suggestions)
                    {
                        int min = Math.Min(title.Length, trie.sug.currentPrefix.Length);
                        int currentL = trie.sug.currentPrefix.Length;
                        int tL = title.Length;
                        if (title.Substring(0, min) == trie.sug.currentPrefix.Substring(0, min) && currentL <= tL)
                        {
                            sugg.Add(title);
                            i++;
                        }
                        if (i == 10)
                        {
                            break;
                        }
                    }
                }
                // Add to cache.
                if (!lettersTyped.Equals("") && sugg.Count != 0)
                {
                    URLCacheDictionary.Add(lettersTyped, sugg);
                }
                return sugg;
            }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string HelloWorld(string name)
        {
            return "Hello " + name;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public String trieTest()
        {
            trie = buildTrie();
            String results = "";
            foreach (Char c in "aaaaa scenic arena")
            {
                results += trie.sug.addNextLetter(c);
            }
            return "Did suggestor add this letter?: " + results +
                   "Current prefix: " + trie.sug.currentPrefix +
                   "Exact match?: " + trie.sug.prefixIsWord().ToString();
        }

        public Trie<String> buildTrie()
        {
            this.trie = new Trie<String>();

            // Retrieve storage account from connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            ConfigurationManager.AppSettings["StorageConnectionString"]);

            // Create blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            CloudBlobContainer container = blobClient.GetContainerReference("demoblobs");

            // Retrieve reference to a blob named "myblob.txt".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference("99lines.txt");

            String text;
            using (var memoryStream = new MemoryStream())
            {
                blockBlob.DownloadToStream(memoryStream);
                text = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            }
            Char[] delimeterArray = { '\n' };
            String[] tArray = text.Split(delimeterArray);
            int i = 0;
            foreach (String s in tArray)
            {
                this.trie.put(s, i.ToString());
                i++;
            }
            return this.trie;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public void filterText()
        {
            string rPattern = "^[a-zA-Z_]+$";
            Regex rx = new Regex(rPattern);

            using (StreamReader sr = new StreamReader(@"C:\enwiki-20141208-all-titles-in-ns0"))
            {
                //using (StreamWriter sw = new StreamWriter(@"C:\output.txt"))
                using (StreamWriter sw = new StreamWriter(@"C:\output1.txt"))
                {
                    while (sr.EndOfStream == false)
                    {
                        string line = sr.ReadLine();
                        if (rx.IsMatch(line))
                        {
                            int i = 0;
                            if (i < 1000)
                            {
                                // Replace all underscores with spaces and format as lower cased string.
                                line = line.Replace("_", " ").ToLower();
                                sw.WriteLine(line);
                                i++;
                            }
                        }
                    }
                }
            }
        }

        public static int db = 0;

        // Builds MySQL Connection String. 
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public String showPlayerStats(String playerName) {
            String[] fullName = playerName.Trim().Split(' ');
            
            String connectionString = ("server=http://54.191.145.110;host=info34401072015.c4njupasq36d.us-west-2.rds.amazonaws.com;Port=3306;Database=NBA;UId=info344user;Password=R00ster.;");
            MySqlConnection connection = new MySqlConnection(connectionString);
            MySqlCommand command = connection.CreateCommand();
            String stats = "";
            try
            {
                db = 0;
                command.CommandText = "SELECT * FROM PLAYERS WHERE PlayerName = '" + playerName + "'";
                connection.Open();
                MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    try
                    {
                        stats += "<img src=\"http://i.cdn.turner.com/nba/nba/.element/img/2.0/sect/statscube/players/large/" + playerName[0] + "_" + playerName[1] + ".png\"" + " alt=\"" + playerName + "\" />" +
                                    "<br>" + reader["PlayerName"] +
                                    "<br>FGP: " + reader["FGP"] +
                                    " TPP: " + reader["TPP"] +
                                    " FTP: " + reader["FTP"] +
                                    " PPG: " + reader["PPG"] +
                                    " GP: " + reader["GP"] +
                                    " <br><a href=\"https://twitter.com/search?q=" + playerName + "&src=typd;\">Twitter</a>" +
                                    " <a href=\"https://www.youtube.com/results?search_query=" + playerName[0] + "+" + playerName[1] + "\">YouTube</a><br>";
                    }
                    catch
                    {
                        stats += "";
                        break;
                    }
                }
            }
            catch
            {
                stats = "";
            }
            connection.Close();
            return stats;

        }
    }
}
