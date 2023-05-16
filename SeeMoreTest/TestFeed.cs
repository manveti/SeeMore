using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using SeeMore;

namespace SeeMoreTest {
    [TestClass]
    public class TestFeed {
        [TestMethod]
        public void testGetUpdateArticles() {
            string feedPath = Path.Join(TestingUtils.testDir, "FeedRss.xml");
            FeedMetadata metadata = new FeedMetadata("Explosm.net", "Daily Comics and more!", feedPath);
            Feed feed = new Feed(Directory.GetCurrentDirectory(), metadata);

            List<Article> expectedArticles = new List<Article>() {
                new Article(
                    "https://explosm.net/comics/sythe",
                    new DateTimeOffset(2023, 5, 15, 14, 32, 35, TimeSpan.Zero),
                    "Comic for 2023.05.15 - Sythe",
                    "New Cyanide and Happiness Comic",
                    "https://explosm.net/comics/sythe"
                ),
                new Article(
                    "https://explosm.net/comics/prescription",
                    new DateTimeOffset(2023, 5, 14, 11, 38, 10, TimeSpan.Zero),
                    "Comic for 2023.05.14 - Prescription",
                    "New Cyanide and Happiness Comic",
                    "https://explosm.net/comics/prescription"
                ),
                new Article(
                    "https://explosm.net/comics/post-nasal-drip",
                    new DateTimeOffset(2023, 5, 13, 0, 0, 10, TimeSpan.Zero),
                    "Comic for 2023.05.13 - Post Nasal Drip",
                    "New Cyanide and Happiness Comic",
                    "https://explosm.net/comics/post-nasal-drip"
                ),
                new Article(
                    "https://explosm.net/comics/hey-god",
                    new DateTimeOffset(2023, 5, 12, 1, 20, 52, TimeSpan.Zero),
                    "Comic for 2023.05.12 - Hey God",
                    "New Cyanide and Happiness Comic",
                    "https://explosm.net/comics/hey-god"
                ),
                new Article(
                    "https://explosm.net/comics/award",
                    new DateTimeOffset(2023, 5, 10, 10, 54, 23, TimeSpan.Zero),
                    "Comic for 2023.05.10 - Award",
                    "New Cyanide and Happiness Comic",
                    "https://explosm.net/comics/award"
                ),
            };
            Dictionary<string, Article> expected = new Dictionary<string, Article>();
            foreach (Article article in expectedArticles) {
                expected[article.id] = article;
            }

            FeedArticles newArticles = feed.getUpdateArticles();
            Assert.AreEqual(newArticles.articles.Count, expected.Count);
            Assert.AreEqual(newArticles.idToGuid.Count, expected.Count);
            foreach (string key in expected.Keys) {
                Assert.IsTrue(newArticles.idToGuid.ContainsKey(key));
                Guid guid = newArticles.idToGuid[key];
                Assert.IsTrue(newArticles.articles.ContainsKey(guid));
                Article expArticle = expected[key];
                Article article = newArticles.articles[guid];
                Assert.AreEqual(article.id, expArticle.id);
                Assert.AreEqual(article.timestamp, expArticle.timestamp);
                Assert.AreEqual(article.title, expArticle.title);
                Assert.AreEqual(article.description, expArticle.description);
                Assert.AreEqual(article.url, expArticle.url);
            }
        }
    }
}
