using System;
using XMLParser;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace XmlParser
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            DateTime startTime = DateTime.Now;
            try
            {
                WikiXmlParser parser = new WikiXmlParser("enwiki-pages-articles-test.xml");
                parser.ExtractPagesAndHierarchy("Pages&Parents.txt", "Categories&Parents.txt", "BiographicalPages.txt");
            } catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            TimeSpan timeSpan = DateTime.Now.Subtract(startTime);
            string elapsedTime = String.Format("{0:00} hours {1:00} minutes {2:00} seconds {3:00} ms", 
                timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
            Console.WriteLine("\rDuration {0}. Press any key to exit...", elapsedTime);
            Console.ReadKey();
        }
    }
}