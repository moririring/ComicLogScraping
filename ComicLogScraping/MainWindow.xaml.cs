using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using Sgml;
using Path = System.IO.Path;

namespace ComicLogScraping
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        static private readonly string Amazon = "http://www.amazon.co.jp";
        static private readonly string AmazonJumpFirst = "/s/ref=sr_nr_p_n_feature_three_br_1?rh=n%3A465392%2Cn%3A%21465610%2Cn%3A466280%2Cn%3A2278488051%2Cp_n_feature_three_browse-bin%3A2128968051&bbn=2278488051&ie=UTF8&qid=1371811066&rnid=2128664051";
        //http://www.amazon.co.jp/gp/search/other?redirect=true&rh=n%3A465392%2Cn%3A%21465610%2Cn%3A466280%2Cn%3A2278488051&bbn=2278488051&pickerToList=lbr_three_browse-bin&ie=UTF8&qid=1372933469&rd=1

        private XDocument GetXDocumentFromHtml(string html)
        {
            using (var sgml = new SgmlReader() { Href = html })
            {
                return XDocument.Load(sgml);
            }
        }

        public string GetLastNumberFromString(string str)
        {
            var max = "";
            var num = "";
            foreach (char c in str)
            {
                if (c >= '0' && c <= '9')
                {
                    num += c;
                }
                else if (num != "")
                {
                    max = (num);
                    num = "";
                }
            }
            return max;
        }
        public string FormatText(string text)
        {
            return  text.Replace(",", "")
                       .Replace(Environment.NewLine, "")
                       .Replace("\\n", "")
                       .Trim();
        }

        public MainWindow()
        {
            InitializeComponent();


            var titlebar = "著者,本タイトル,出版社,巻数,価格,本の種類,言語,ISBN10,ISBN13,発売日,リンク,画像,内容" + Environment.NewLine;

            Assembly myAssembly = Assembly.GetEntryAssembly();
            string path = Path.GetDirectoryName(myAssembly.Location);
            File.WriteAllText(Path.Combine(path, "jump.csv"), titlebar, Encoding.UTF8);
            File.WriteAllText(Path.Combine(path, "log.txt"), "", Encoding.UTF8);


            var nextUrl = AmazonJumpFirst;
            while (true)
            {
                XDocument topXml = GetXDocumentFromHtml(Amazon + nextUrl);
                var topNs = topXml.Root.Name.Namespace;
                var comics = topXml.Descendants(topNs + "div")
                               .Where(e => e.Attribute("id") != null && e.Attribute("id").Value.Contains("result_"));
                
                //内容
                var comiclog = new StringBuilder();

                /*
                Parallel.ForEach(comics, comic =>
                    {

                    });
                */

                foreach (var comic in comics)
                {
                    var oneComic = comic.Descendants("div")
                                    .Where(e => e.Attribute("class") != null && e.Attribute("class").Value == "image imageContainer")
                                    .Descendants(topNs + "a")
                                    .Select(e => e.Attribute("href").Value)
                                    .FirstOrDefault();

                    if (!string.IsNullOrEmpty(oneComic))
                    {
                        XDocument oneXml = GetXDocumentFromHtml(oneComic);
                        var oneNs = oneXml.Root.Name.Namespace;
                        var imageLink = oneXml.Descendants(oneNs + "td")
                                          .Where(e => e.Attribute("id") != null && e.Attribute("id").Value.Contains("prodImageCell"))
                                          .Descendants("img")
                                          .Select(e => e.Attribute("src").Value)
                                          .FirstOrDefault();
                        imageLink = FormatText(imageLink);

 
                        var keywords = oneXml.Descendants(oneNs + "meta")
                                       .Where(e => e.Attribute("name") != null && e.Attribute("name").Value.Contains("keywords"))
                                       .Select(e => e.Attribute("content").Value).FirstOrDefault();
                        //著者を,でつないで居るのでそこだけ置換
                        keywords = keywords.Replace(", ", " ");


                        var titles = keywords.Split(',');
                        var ebook = titles[0].Contains("ebook");
                        string auther, bookTitle, publisher, bookNumber;
                        if (ebook)
                        {
                            auther = titles[1];
                            bookTitle = titles[2];
                            publisher = titles[3];
                            bookNumber = GetLastNumberFromString(titles[2]);
                        }
                        else
                        {
                            auther = titles[0];
                            bookTitle = titles[1];
                            publisher = titles[2];
                            bookNumber = GetLastNumberFromString(titles[1]);
                        }
                        //kindle book
                        if (!auther.Contains(" セット ") && !auther.Contains("完結セット") && !auther.Contains("コミックセット") && !auther.Contains("巻セット")
                            && !bookTitle.Contains(" セット ") && !bookTitle.Contains("完結セット") && !bookTitle.Contains("コミックセット") && !bookTitle.Contains("巻セット"))
                        {
                            var pricev = oneXml.Descendants(oneNs + "b")
                                      .FirstOrDefault(e => e.Attribute("class") != null && e.Attribute("class").Value.Contains("priceLarge"));
                            var price = pricev == null ? "" : FormatText(pricev.Value);

                            //新刊にはないみたい内容紹介
                            var contentv = oneXml.Descendants(oneNs + "div")
                                      .FirstOrDefault(e => e.Attribute("class") != null && e.Attribute("class").Value.Contains("productDescriptionWrapper"));
                            var content = contentv == null ? "" : FormatText(contentv.Value);



                            var lis = oneXml.Descendants(oneNs + "div")
                                                .Where(e => e.Attribute("class") != null && e.Attribute("class").Value.Contains("content"))
                                                .Descendants("li");


                            var lan = "";
                            var isb10 = "";
                            var isb13 = "";
                            var sale = "";

                            foreach (var li in lis)
                            {
                                if (li.Value.Contains("出版社:"))
                                {
                                    var coms = li.Value.Split(' ');
                                    sale = coms[coms.Length - 1];
                                    sale = sale.Substring(sale.IndexOf('(') + 1, sale.Length - sale.IndexOf('(') - 2);
                                }
                                else if (li.Value.Contains("言語"))
                                {
                                    var coms = li.Value.Split(' ');
                                    lan = FormatText(coms[1]);
                                }
                                else if (li.Value.Contains("ISBN-10") || li.Value.Contains("ASIN"))
                                {
                                    var coms = li.Value.Split(' ');
                                    isb10 = FormatText(coms[1]);
                                }
                                else if (li.Value.Contains("ISBN-13"))
                                {
                                    var coms = li.Value.Split(' ');
                                    isb13 = FormatText(coms[1]);
                                }
                            }

                            comiclog.Append(auther + ",");
                            comiclog.Append(bookTitle + ",");
                            comiclog.Append(publisher + ",");
                            comiclog.Append(bookNumber + ",");
                            comiclog.Append(price + ",");
                            comiclog.Append(ebook + ",");
                            comiclog.Append(lan + ",");
                            comiclog.Append(isb10 + ",");
                            comiclog.Append(isb13 + ",");
                            comiclog.Append(sale + ",");
                            comiclog.Append(oneComic + ",");
                            comiclog.Append(imageLink + ",");
                            comiclog.Append(content + ",");
                            comiclog.AppendLine("");
                        }
                    }
                }
                File.AppendAllText(Path.Combine(path, "jump.csv"), comiclog.ToString(), Encoding.UTF8);
                //次のページへ
                nextUrl = topXml.Descendants(topNs + "a")
               .Where(e => e.Attribute("class") != null && e.Attribute("class").Value.Contains("pagnNext"))
               .Select(e => e.Attribute("href").Value).FirstOrDefault();

                File.AppendAllText(Path.Combine(path, "log.txt"), comics.Count() + "," + nextUrl + Environment.NewLine, Encoding.UTF8);

                //なかったら終わり
                if (string.IsNullOrWhiteSpace(nextUrl)) break;
            }
        }
    }
}
