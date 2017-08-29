using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Office.Server.Search.WebControls;
using Microsoft.SharePoint.Utilities;
using System.Xml;
using System.Collections;
using System.Globalization;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Microsoft.SharePoint;
using System.Web;

namespace MultiValueFilterGenerator
{
    class MultiValueFilterGenerator : RefinementFilterGenerator
    {

        public MultiValueFilterGenerator()
        {
        }

        public override List<System.Xml.XmlElement> GetRefinement(Dictionary<string, Dictionary<string, RefinementDataElement>> refinedData,
                                                                    System.Xml.XmlDocument filterXml,
                                                                    int maxFilterCats)
        {

            List<XmlElement> result = new List<XmlElement>();
    
            foreach (FilterCategory category in base._Categories)
            {

                long itemCount = 0L;

                Dictionary<string, RefinementDataElement> filterResults = refinedData.ContainsKey(category.MappedProperty) ? refinedData[category.MappedProperty] : new Dictionary<string, RefinementDataElement>();

                Dictionary<string, RefinementDataElement> newResults = new Dictionary<string, RefinementDataElement>();

                //rebuild results
                long resultSum = 0;
                foreach (RefinementDataElement item in filterResults.Values)
                {
                    string[] actual = item.FilterDisplayValue.Split(';');
                    foreach (string x in actual)
                    {
                        RefinementDataElement element;
                        if (newResults.ContainsKey(x))
                        {
                            element = newResults[x];
                        }
                        else
                        {
                            element = new RefinementDataElement(x, 0, 0);
                            newResults.Add(x, element);
                        }
                        element.FilterValueCount = element.FilterValueCount + item.FilterValueCount;
                        resultSum = resultSum + item.FilterValueCount;
                        itemCount = (long)(itemCount + item.FilterValueCount);
                    }
                }


                if (itemCount >= category.MetadataThreshold && itemCount != 0)
                {

                    //Calculate precentages
                    foreach (RefinementDataElement item in newResults.Values)
                    {
                        item.FilterValuePercentage = item.FilterValueCount / resultSum;
                    }

                    //Order by percentage
                    IEnumerable<RefinementDataElement> topResults = newResults.Values.OrderBy(i => i.FilterValuePercentage);

                    XmlElement element = filterXml.CreateElement("FilterCategory");
                    element.SetAttribute("Id", category.Id);
                    element.SetAttribute("ConfigId", category.Id);
                    element.SetAttribute("Type", category.FilterType);
                    element.SetAttribute("DisplayName", RefinementFilterGenerator.TruncatedString(category.Title, base.NumberOfCharsToDisplay));
                    element.SetAttribute("ManagedProperty", category.MappedProperty);
                    element.SetAttribute("ShowMoreLink", category.ShowMoreLink);
                    element.SetAttribute("FreeFormFilterHint", category.FreeFormFilterHint);
                    element.SetAttribute("MoreLinkText", category.MoreLinkText);
                    element.SetAttribute("LessLinkText", category.LessLinkText);
                    element.SetAttribute("ShowCounts", category.ShowCounts);

                    XmlElement containerElmt = filterXml.CreateElement("Filters");

                    string url = string.Empty;

                    bool selectable = BuildRefinementUrl(category, string.Empty, out url);

                    containerElmt.AppendChild(GenerateFilterElement(filterXml,
                        RefinementFilterGenerator.TruncatedString("Any " + category.Title, base.NumberOfCharsToDisplay),
                        url, selectable, "",
                        "",
                        "",
                        ""));

                    foreach (RefinementDataElement item in topResults.Take(category.NumberOfFiltersToDisplay))
                    {

                        selectable = BuildRefinementUrl(category, item.FilterDisplayValue, out url);

                        containerElmt.AppendChild(GenerateFilterElement(filterXml,
                            RefinementFilterGenerator.TruncatedString(item.FilterDisplayValue, base.NumberOfCharsToDisplay),
                            url, selectable, item.FilterDisplayValue,
                            item.FilterValueCount.ToString(),
                            item.FilterValuePercentage.ToString(),
                            ""));
                    }

                    element.AppendChild(containerElmt);
                    result.Add(element);
                }
            }
            return result;

        }
        private static XmlElement GenerateFilterElement(XmlDocument filterXml,
            string truncatedFilterDisplayValue,
            string url,
            bool selectable,
            string filterTooltip,
            string count,
            string percentage,
            string filterIndentation)
        {
            XmlElement element2 = filterXml.CreateElement("Filter");
            XmlElement newChild = filterXml.CreateElement("Value");
            newChild.InnerText = truncatedFilterDisplayValue;
            element2.AppendChild(newChild);
            newChild = filterXml.CreateElement("Tooltip");
            newChild.InnerText = filterTooltip;
            element2.AppendChild(newChild);
            newChild = filterXml.CreateElement("Url");
            newChild.InnerText = url;
            element2.AppendChild(newChild);
            newChild = filterXml.CreateElement("Selection");
            if (selectable)
            {
                newChild.InnerText = "Deselected";
            }
            else
            {
                newChild.InnerText = "Selected";
            }
            element2.AppendChild(newChild);
            newChild = filterXml.CreateElement("Count");
            newChild.InnerText = count;
            element2.AppendChild(newChild);
            newChild = filterXml.CreateElement("Percentage");
            newChild.InnerText = percentage;
            element2.AppendChild(newChild);
            if (!string.IsNullOrEmpty(filterIndentation))
            {
                newChild = filterXml.CreateElement("Indentation");
                newChild.InnerText = filterIndentation.ToString();
                element2.AppendChild(newChild);
            }
            return element2;
        }

        protected bool BuildRefinementUrl(FilterCategory fc, string value, out string url)
        {
            string filteringProperty = fc.MappedProperty;
            bool notSelected = false;
            string origFilter = string.Empty;
            bool hasRefinement = false;

            hasRefinement = (HttpContext.Current.Request.QueryString["r"] != null);

            if (!hasRefinement)
            {
                origFilter = string.Empty;
            }
            else
            {
                origFilter = HttpContext.Current.Request.QueryString["r"];
            }

            string filterString = origFilter;

            if (string.IsNullOrEmpty(value))
            {
                //Remove any filters for this category
                int num = filterString.Length;
                filterString = this.RemoveCategoryFromUrl(filterString, fc);
                //if teh value changed then all values were not selected
                notSelected = (bool)(filterString.Length != num);
            }
            else
            {
                notSelected = false;
                string propertyValue = filteringProperty.ToLower() + ":" + value;

                if (!filterString.Contains(propertyValue))
                {
                    notSelected = true;
                }

                if (notSelected)
                {
                    Regex regex = new Regex(string.Format("(((({0})(:|>|<|<=|>=|=)\"([^\"]|\"\")*\"))|((({0})(:|>|<|<=|>=|=)([^\\s]*))))((\\s+AND\\s+)(((({0})(:|>|<|<=|>=|=)\"([^\"]|\"\")*\"))|((({0})(:|>|<|<=|>=|=)([^\\s]*)))))*", filteringProperty), RegexOptions.IgnoreCase);
                    MatchCollection matchs2 = regex.Matches(filterString);
                    StringBuilder builder2 = new StringBuilder();
                    builder2.Append(" " + propertyValue);
                    foreach (Match match2 in matchs2)
                    {
                        builder2.Append(" AND ");
                        builder2.Append(match2.Value);
                    }
                    filterString = regex.Replace(filterString, string.Empty) + builder2.ToString();
                }
                else
                {
                    StringBuilder builder = new StringBuilder();
                    Regex regex2 = new Regex(string.Format("({0}(?<Operator>:|>|<|<=|>=|=)\"(?<FilterValue>([^\"]|\"\")*)\"(\\s|$))|({0}(?<Operator>:|>|<|<=|>=|=)(?<FilterValue>[^\\s]*)(\\s|$))", filteringProperty), RegexOptions.IgnoreCase);
                    //get a list of values for this category
                    foreach (Match match in regex2.Matches(filterString))
                    {
                        if ((match != null) && !string.IsNullOrEmpty(match.Value))
                        {
                            string trimmedValue = match.Value.Trim();
                            if (trimmedValue!=propertyValue)
                            {
                                if (builder.Length > 0)
                                {
                                    builder.Append(" AND ");
                                }
                                builder.Append(trimmedValue);
                            }
                        }
                    }
                    filterString = this.RemoveCategoryFromUrl(filterString, fc);
                    if (builder.Length > 0)
                    {
                        filterString = filterString + " " + builder.ToString();
                    }
                }
            }

            filterString = HttpUtility.UrlEncode(filterString.Trim());
            string originalUrl = HttpContext.Current.Request.Url.OriginalString;

            Uri request = HttpContext.Current.Request.Url;
            NameValueCollection queryString = HttpContext.Current.Request.QueryString;

            originalUrl = request.OriginalString.Replace(request.Query, string.Empty);
            string qs = string.Empty;

            foreach (string key in queryString.AllKeys)
            {
                if (key != "r")
                {
                    qs = qs + "&" + key + "=" + queryString[key].ToString();
                }
            }

            qs = qs + "&r=" + filterString;

            url = originalUrl + "?" + qs.Substring(1);

            return notSelected;
        }

        private string RemoveCategoryFromUrl(string currentUrl, FilterCategory fc)
        {
            string filteringProperty = fc.MappedProperty;
            string expression = string.Empty;

            if (fc.CustomFiltersConfiguration != null)
            {
                expression = string.Format(CultureInfo.InvariantCulture, "({0}(?<Operator>:|>|<|<=|>=|=)\"(?<FilterValue>([^\"]|\"\")*)\"(\\s|$))|({0}(?<Operator>:|>|<|<=|>=|=)(?<FilterValue>[^\\s]*)(\\s|$))", new object[] { filteringProperty });
            }
            else
            {
                expression = string.Format(CultureInfo.InvariantCulture, "(((({0})(:|>|<|<=|>=|=)\"([^\"]|\"\")*\"))|((({0})(:|>|<|<=|>=|=)([^\\s]*))))((\\s+AND\\s+)(((({0})(:|>|<|<=|>=|=)\"([^\"]|\"\")*\"))|((({0})(:|>|<|<=|>=|=)([^\\s]*)))))*", new object[] { filteringProperty });
            }
            Regex regex = new Regex(expression, RegexOptions.IgnoreCase);
            return regex.Replace(currentUrl, string.Empty);
        }

    }
}

