using System.IO;
using System.Runtime.CompilerServices;

namespace SeeMoreTest {
    public class TestingUtils {
        private static string _testDir = null;
        public static string testDir {
            get {
                if (_testDir == null) {
                    string ourPath = callerPath();
                    _testDir = Directory.GetParent(ourPath).FullName;
                }
                return _testDir;
            }
        }

        public static string callerPath([CallerFilePath] string path = "") {
            return path;
        }
    }
}