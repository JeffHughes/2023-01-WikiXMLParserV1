using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;

namespace XMLParser
{
    /// A class for parsing English-language Wikipedia XML dump.
    public class WikiXmlParser
    {
        private XmlReader xmlReader;

        /// <summary>Constructor of XmlParser class</summary>    
        /// <param name="pathToXMLDumpFile">Path to Wikipedia XML dump file</param>
        public WikiXmlParser(string pathToXMLDumpFile)
        {
            xmlReader = XmlReader.Create(pathToXMLDumpFile);
        }

        /// <summary>Reads next page from XML dump</summary> 
        /// <returns>Next page or null if no more pages.</returns>
        public Page GetNextPage()
        {
            while (xmlReader.ReadToFollowing("page"))
            {
                // Some pages just perform redirection to actual page. We skip such pages. 
                bool isRedirection = false;
                Page page = new Page();
                int ns = -1;
                XmlReader r = xmlReader.ReadSubtree();
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element)
                    {
                        if (r.Name == "redirect")
                        {
                            isRedirection = true;
                            break;
                        } 
                        // Other tags like <id>, <revision>, <timestamp>, <contributor>,... may be retrieved in the similar way
                        if (r.Name == "title")
                        {
                            r.Read();
                            page.Title = r.Value;
                        }
                        else if (r.Name == "ns")
                        {
                            r.Read();
                            if (int.TryParse(r.Value, out ns))
                            {
                                page.Namespace = ns;
                             }
                        }
                        else if (r.Name == "text")
                        {
                            r.Read();
                            page.Text = r.Value;
                        }
                    }
                }
                if (!isRedirection && !String.IsNullOrWhiteSpace(page.Title) && !String.IsNullOrWhiteSpace(page.Text))
                {
                    return page;
                }
            }
            // No more pages
            return null;
        }


        /// <summary>Extracts integer numbers from string.</summary>
        /// <param name="s">Input string.</param>
        /// <returns>Array of int</param>
        private static int[] GetNumbers(string s)
        {
            return Regex.Split(s, @"\D+").Where(t => !String.IsNullOrEmpty(t)).Select(int.Parse).ToArray();
        }

        /// <summary>Extracts birth or death year from proper category string.</summary>
        /// <param name="category">Input category string.</param>
        /// <param name="year">A year, if extracted.</param>
        /// <param name="birth">Boolean, specifies if birth date sould be extracted, death date otherwise.</param>
        /// <returns>True if success, false otherwise.</returns>
        private bool BirthDeathYear(string category, ref int year, bool birth) 
        {
            string token = birth ? "births" : "deaths";
            if (category.IndexOf(token) < 0)
            {
                return false;
            }
            // Date may be 
            bool bc = Regex.Match(category, @"\bBC\b").Success;
            int[] numbers = GetNumbers(category);
            // Date may have BC (Before Christ) specifier. If "Century"  or "millennium" mentioned, this is not a year.
            if (numbers.Length != 1 || category.IndexOf("century") > 0 || category.IndexOf("animal") > 0 || category.IndexOf("millennium") > 0)
            {
                return false;
            }
            else
            {
                year = bc ? -numbers[0] : numbers[0];
                return true;
            }
        }

        /// <summary>Reads Wiki XML and creates several files.</summary>
        /// <param name="pathPagesAndParents">Path to tab-delimited output file: page titles, number of external references to page, and parent1|parent2|...</param>
        /// <param name="pathCategoriesAndParents">Path to tab-delimited output file: categories titles, and parent1|parent2|...</param>
        /// <param name="pathBiographicalPages">Path to tab-delimited output file: titles of biographical pages, birth year, death year, age, number of external references, and parent1|parent2|...</param>
        /// <returns>Nothing.</returns>
        public void ExtractPagesAndHierarchy(string pathPagesAndParents, string pathCategoriesAndParents, string pathBiographicalPages)
        {
            // Column, containing number of links to a given page from other pages may be calculated only after parser finishes.
            // Temporary files are almost the same as final ones, but withoun this columnn.
            string tempPagesAndParents = Path.GetTempFileName();
            string tempBiographicalPages = Path.GetTempFileName();

            // <page title, number of links to it>
            var linksCount = new Dictionary<string, int>();

            using (StreamWriter swc = new StreamWriter(pathCategoriesAndParents, false, Encoding.UTF8))
            using (StreamWriter sw = new StreamWriter(tempPagesAndParents, false, Encoding.UTF8))
            using (StreamWriter swb= new StreamWriter(tempBiographicalPages, false, Encoding.UTF8))
            {
                int nPages = 0;
                Page page;
                while ((page = GetNextPage()) != null)
                {
                    nPages++;
                    if (page.Namespace == 0)
                    {
                        // We are writing tab delimited files. Each line is in the following format:
                        // Title CategoryParent|CategoryParent2|CategoryParent3...
                        string line = page.Title + "\t";

                        // Mark years as not processed yet or not presented in a page with int.MinValue
                        int birthYear = int.MinValue, deathYear = int.MinValue;
                        foreach (var category in page.Parents)
                        {
                            if (birthYear == int.MinValue)
                            {
                                BirthDeathYear(category, ref birthYear, true);
                            }
                            if (deathYear == int.MinValue)
                            {
                                BirthDeathYear(category, ref deathYear, false);
                            }
                            line += category + "|";
                        }

                        // Count number of times each page title is referenced in other pages.
                        // So, we incrememnt linksCount[title]  for every link title.
                        foreach (var title in page.Links)
                        {
                            int count;
                            if (linksCount.TryGetValue(title, out count))
                            {
                                linksCount[title] += 1;
                            }
                            else
                            {
                                linksCount.Add(title, 1);
                            }
                        }
                        if (page.Parents.Count > 0) // Exclude disambiguation pages
                        {
                            Console.Write("\r{0}", nPages.ToString().PadRight(10));
                            sw.WriteLine(line);
                            if ((birthYear != int.MinValue || deathYear != int.MinValue) && page.Title.IndexOf("List of") != 0)
                            {
                                string[] parts = line.Split('\t');
                                swb.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", parts[0],
                                    (birthYear != int.MinValue ? birthYear.ToString() : ""),
                                    (deathYear != int.MinValue ? deathYear.ToString() : ""),
                                    (birthYear != int.MinValue && deathYear != int.MinValue ? (deathYear - birthYear).ToString() : ""),
                                    parts[1]);
                            }
                        }
                    }
                    else if (page.Namespace == 14)
                    {
                        const string prefix = "Category:";
                        int i = page.Title.IndexOf(prefix);
                        if (i == 0)
                        {
                            string line = page.Title.Substring(prefix.Length) + "\t";
                            foreach (var s in page.Parents)
                            {
                                line += s + "|";
                            }
                            swc.WriteLine(line);
                        }
                    }
                    // Process other namespaces if needed
                }
            }

            // sw inserts numbers of external links to the data, read from tempPagesAndParents.
            using (StreamReader sr = new StreamReader(tempPagesAndParents, Encoding.UTF8))
            using (StreamWriter sw = new StreamWriter(pathPagesAndParents, false, Encoding.UTF8))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    int n;
                    sw.WriteLine("{0}\t{1}\t{2}", parts[0], linksCount.TryGetValue(parts[0], out n) ? n.ToString() : " 0", parts[1]);
                }
            }

            // sw inserts numbers of external links to the data, read from tempBiographicalPages
            using (StreamReader sr = new StreamReader(tempBiographicalPages, Encoding.UTF8))
            using (StreamWriter sw = new StreamWriter(pathBiographicalPages, false, Encoding.UTF8))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    int n;
                    sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                        parts[0], // title
                        parts[1], // birth year
                        parts[2], // death year
                        parts[3], // age
                        linksCount.TryGetValue(parts[0], out n) ? n.ToString() : "0", // number of distinct links to this page from others
                        parts[4] //parent categories
                    );
                }
            }

            // CLeanup
            File.Delete(tempPagesAndParents);
            File.Delete(tempBiographicalPages);
        }
    }
}
