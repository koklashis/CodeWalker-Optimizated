using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CodeWalker.GameFiles
{
    public class JenkHash
    {
        public JenkHashInputEncoding Encoding { get; set; }
        public string Text { get; set; }
        public int HashInt { get; set; }
        public uint HashUint { get; set; }
        public string HashHex { get; set; }

        public JenkHash(string text, JenkHashInputEncoding encoding)
        {
            Encoding = encoding;
            Text = text;
            HashUint = GenHash(text, encoding);
            HashInt = (int)HashUint;
            HashHex = "0x" + HashUint.ToString("X");
        }

        public static uint GenHash(string text, JenkHashInputEncoding encoding)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            uint h = 0;

            if (encoding == JenkHashInputEncoding.ASCII)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    h += (byte)text[i];
                    h += (h << 10);
                    h ^= (h >> 6);
                }
            }
            else
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c < 0x80)
                    {
                        h += (byte)c;
                        h += (h << 10);
                        h ^= (h >> 6);
                    }
                    else if (c < 0x800)
                    {
                        h += (byte)(0xC0 | (c >> 6));
                        h += (h << 10);
                        h ^= (h >> 6);
                        h += (byte)(0x80 | (c & 0x3F));
                        h += (h << 10);
                        h ^= (h >> 6);
                    }
                    else if (char.IsSurrogate(c) && i + 1 < text.Length && char.IsSurrogatePair(c, text[i + 1]))
                    {
                        int codePoint = char.ConvertToUtf32(c, text[++i]);
                        h += (byte)(0xF0 | (codePoint >> 18));
                        h += (h << 10);
                        h ^= (h >> 6);
                        h += (byte)(0x80 | ((codePoint >> 12) & 0x3F));
                        h += (h << 10);
                        h ^= (h >> 6);
                        h += (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                        h += (h << 10);
                        h ^= (h >> 6);
                        h += (byte)(0x80 | (codePoint & 0x3F));
                        h += (h << 10);
                        h ^= (h >> 6);
                    }
                    else
                    {
                        h += (byte)(0xE0 | (c >> 12));
                        h += (h << 10);
                        h ^= (h >> 6);
                        h += (byte)(0x80 | ((c >> 6) & 0x3F));
                        h += (h << 10);
                        h ^= (h >> 6);
                        h += (byte)(0x80 | (c & 0x3F));
                        h += (h << 10);
                        h ^= (h >> 6);
                    }
                }
            }

            h += (h << 3);
            h ^= (h >> 11);
            h += (h << 15);

            return h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GenHash(string text)
        {
            if (text == null) return 0;
            uint h = 0;
            for (int i = 0; i < text.Length; i++)
            {
                h += (byte)text[i];
                h += (h << 10);
                h ^= (h >> 6);
            }
            h += (h << 3);
            h ^= (h >> 11);
            h += (h << 15);

            return h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GenHash(byte[] data)
        {
            if (data == null) return 0;
            uint h = 0;
            for (uint i = 0; i < data.Length; i++)
            {
                h += data[i];
                h += (h << 10);
                h ^= (h >> 6);
            }
            h += (h << 3);
            h ^= (h >> 11);
            h += (h << 15);
            return h;
        }
    }

    public enum JenkHashInputEncoding
    {
        UTF8 = 0,
        ASCII = 1,
    }

    public class JenkIndMatch
    {
        public string Hash { get; set; }
        public string Value { get; set; }
        public double Score { get; set; }

        public JenkIndMatch(string hash, string val)
        {
            Hash = hash;
            Value = val;
            CalculateScore();
        }

        public void CalculateScore()
        {
            int wordlength = 0;
            int wordrank = 0;

            string okwordsymbs = " _-.";
            string goodwordsymbs = "_";

            for (int i = 0; i < Value.Length; i++)
            {
                char c = Value[i];

                bool wordchar = (char.IsLetter(c) || char.IsDigit(c) || goodwordsymbs.Contains(c));

                if (wordchar)
                {
                    wordlength++;
                }
                else if (okwordsymbs.Contains(c))
                {
                    //wordlength++;
                }
                else
                {
                    if (wordlength > 2)
                    {
                        wordrank += wordlength;
                    }
                    wordlength = 0;
                }
            }
            if (wordlength > 2)
            {
                wordrank += wordlength;
            }

            if (Value.Length > 0)
            {
                double n = (double)Value.Length;
                Score = (((double)wordrank) / n);
            }
            else
            {
                Score = 0.0;
            }
        }

        public override string ToString()
        {
            return string.Format("{0} -> {1}   ({2:0.##})", Hash, Value, Score);
        }
    }

    public class JenkIndProblem
    {
        public string Filename { get; set; }
        public string Excuse { get; set; }
        public int Line { get; set; }

        public JenkIndProblem(string filepath, string excuse, int line)
        {
            Filename = Path.GetFileName(filepath);
            Excuse = excuse;
            Line = line;
        }
        public override string ToString()
        {
            return string.Format("{0} : {1} at line {2}", Filename, Excuse, Line);
        }
    }

    public static class JenkIndex
    {
        public static Dictionary<uint, string> Index = new Dictionary<uint, string>();
        private static object syncRoot = new object();

        public static void Clear()
        {
            lock (syncRoot)
            {
                Index.Clear();
            }
        }

        public static bool Ensure(string str)
        {
            uint hash = JenkHash.GenHash(str);
            if (hash == 0) return true;
            lock (syncRoot)
            {
                if (!Index.ContainsKey(hash))
                {
                    Index.Add(hash, str);
                    return false;
                }
            }
            return true;
        }

        public static string GetString(uint hash)
        {
            string res;
            lock (syncRoot)
            {
                if (!Index.TryGetValue(hash, out res))
                {
                    res = hash.ToString();
                }
            }
            return res;
        }

        public static string TryGetString(uint hash)
        {
            string res;
            lock (syncRoot)
            {
                if (!Index.TryGetValue(hash, out res))
                {
                    res = string.Empty;
                }
            }
            return res;
        }

        public static string[] GetAllStrings()
        {
            string[] res = null;
            lock (syncRoot)
            {
                res = Index.Values.ToArray();
            }
            return res;
        }
    }
}
