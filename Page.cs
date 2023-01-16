using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XMLParser
{
    public class Page
    {
        /// <summary>Page title element.</summary>
        public string Title
        {
            get
            {
                return title;
            }
            set
            {
                title = value;
            }
        }

        /// <summary>Page text element.</summary>
        public string Text
        {
            get
            {
                return text;
            }
            set
            {
                text = value;
            }
        }

        /// <summary>Value of current page namespace.</summary>
        public int Namespace
        {
            get
            {
                return ns;
            }
            set
            {
                ns = value;
            }

        }

        /// <summary>Titles of articles, referenced in current page.</summary>
        /// <returns>HashSet of titles of links.</returns>
        public HashSet<string> Links
        {
            get
            {
                if (links == null)
                {
                    GetReferences();
                }
                return links;
            }
        }

        /// <summary>Titles of parents current page belongs to.</summary>
        /// <returns>HashSet of titles of page parents</returns>
        public HashSet<string> Parents
        {
            get
            {
                if (parents == null)
                {
                    GetReferences();
                }
                return parents;
            }
        }

        /// <summary>Extracts keys and non-empty values from page Infobox</summary>    
        /// <param name="title">Title of infobox, out</param>
        /// <param name="keysAndValues">List of keys and values, out</param>
        /// <returns>Returns false if no infobox keys and values found, true otherwise.</returns>
        public bool ParseInfobox(out string title, out List<KeyValuePair<string, string>> keysAndValues)
        {
            const string infobox = "{{Infobox";
            // Infobox is part of text starting with "{{Infobox" and ending with "\n}}\n" line
            // It contains key-value pairs in lines of format |key = value
            keysAndValues = new List<KeyValuePair<string, string>>();
            title = "";
            int i = Text.IndexOf(infobox);
            if (i >= 0)
            {
                int j = Text.IndexOf("\n}}\n", i);
                if (j > i)
                {
                    int k = Text.IndexOf("\n", i); // Always > i
                    title = Text.Substring(i + infobox.Length, k - i - infobox.Length).Trim();
                    string[] parts = Text.Substring(k, j - k).Trim().Split('|');
                    foreach (string part in parts)
                    {
                        string[] kv = part.Trim().Split('=');
                        if (kv.Length == 2 && !String.IsNullOrWhiteSpace(kv[1]))
                        {
                            keysAndValues.Add(new KeyValuePair<string, string>(kv[0].TrimStart('|').Trim(), kv[1].Trim()));
                        }
                    }
                    return keysAndValues.Count > 0;
                }
                return false;
            }
            return false;
        }

        private string title, text;

        // Links to wikipedia items are shortcuts URLs like https://en.wikipedia.org/wiki/TitleOfItemToGo. We store TitleOfItemToGo only.
        // links contains TitleOfItemToGo for links to other Wikipedia pages
        // parents contains titles of parent categories 
        private HashSet<string> links, parents;

        // Wikipedia's internal namespace of page, like 0 for articles and 14 for parents.
        // See <namespaces> element at the beginning of XML dump for details.
        private int ns;

        /// <summary>Calculates distinct hyperlinks to other pages</summary>
        private void GetReferences()
        {
            HashSet<string> references = new HashSet<string>();
            links = new HashSet<string>();
            parents = new HashSet<string>();
            // Format of links is [[LinkString]]
            string pattern = string.Format("{0}{1}{2}", Regex.Escape("[["), ".+?", Regex.Escape("]]"));
            foreach (Match m in Regex.Matches(text, pattern))
            {
                // Link may contain attributes for display, for instance "Hierarchy|hierarchical"
                // The first part before '|' is page title, others (if are) are off interest here.
                string reference = m.Value.TrimStart('[').TrimEnd(']').Split('|')[0];
                int i;

                // reference may contain "File:" or "Image:", etc. prefix. It is not a link to a page or to category if so.
                if ((i = reference.IndexOf(':')) >= 0)
                {
                    if (reference.IndexOf("Category:") == 0)
                    {
                        parents.Add(reference.Substring(i + 1).Trim());
                    }
                }
                else
                {
                    links.Add(reference.Trim());
                }
            }
        }
    }
}
