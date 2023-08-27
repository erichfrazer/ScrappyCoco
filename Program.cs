using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Runtime.Intrinsics;

namespace ScrappyCoco
{
    public static class EnumeratorCloner
    {
        public static T Clone<T>(T source) where T : class, IEnumerator<T>
        {
            var sourceType = source.GetType().UnderlyingSystemType;
            var sourceTypeConstructor = sourceType.GetConstructor(new Type[] { typeof(Int32) });
            var newInstance = sourceTypeConstructor.Invoke(new object[] { -2 }) as T;

            var nonPublicFields = source.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            var publicFields = source.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in nonPublicFields)
            {
                var value = field.GetValue(source);
                field.SetValue(newInstance, value);
            }
            foreach (var field in publicFields)
            {
                var value = field.GetValue(source);
                field.SetValue(newInstance, value);
            }
            return newInstance;
        }
    }

    class StartStop : IComparable<StartStop>
    {
        public int Start;
        public int Stop;
        public bool Dead;

        public StartStop( int start, int stop)
        {
            Start = start;
            Stop = stop;
            Dead = false;
        }

        public int CompareTo(StartStop? other)
        {
            return this.Start - other.Start;
        }
    }

    class Compute
    {
        Dictionary<int, SortedSet<StartStop>> VStrokes = new Dictionary<int, SortedSet<StartStop>>();
        Dictionary<int, SortedSet<StartStop>> HStrokes = new Dictionary<int, SortedSet<StartStop>>();

        public void Go(char[] dirs, int [] lens)
        {
            int x = 0;
            int y = 0;

            for (int i = 0; i < dirs.Length; i++)
            {
                char ch = dirs[i];
                int len = lens[i];

                switch(ch)
                {
                    case 'U':
                        {
                            bool b = VStrokes.TryGetValue(x, out SortedSet<StartStop> vss);
                            if (!b)
                            {
                                vss = new SortedSet<StartStop>();
                                VStrokes[x] = vss;
                            }
                            vss.Add(new StartStop(y, y + len));
                            y += len;
                            break;
                        }
                    case 'D':
                        {
                            bool b = VStrokes.TryGetValue(x, out SortedSet<StartStop> vss);
                            if (!b)
                            {
                                vss = new SortedSet<StartStop>();
                                VStrokes[x] = vss;
                            }
                            vss.Add(new StartStop(y - len, y));
                            y -= len;
                            break;
                        }
                    case 'L':
                        {
                            bool b = HStrokes.TryGetValue(y, out SortedSet<StartStop> hss);
                            if (!b)
                            {
                                hss = new SortedSet<StartStop>();
                                HStrokes[y] = hss;
                            }
                            hss.Add(new StartStop(x - len, x));
                            x -= len;
                            break;
                        }
                    case 'R':
                        {
                            bool b = HStrokes.TryGetValue(y, out SortedSet<StartStop> hss);
                            if (!b)
                            {
                                hss = new SortedSet<StartStop>();
                                HStrokes[y] = hss;
                            }
                            hss.Add(new StartStop(x, x + len));
                            x += len;
                            break;
                        }
                }
            }

            // consolidate multiple strokes. This is going to be O(N)*C
            foreach (KeyValuePair<int,SortedSet<StartStop>> kvp in HStrokes)
            {
                if( kvp.Value.Count < 2 )
                {
                    continue;
                }
                List<StartStop> toRemove = new List<StartStop>();
                int strokesCount = kvp.Value.Count;
                for (int stroke = 0; stroke < strokesCount; stroke++)
                {
                    var v = kvp.Value.GetEnumerator();
                    int stroke2 = stroke + 1;
                    while( stroke2 > 0)
                    {
                        v.MoveNext();
                        stroke2--;
                    }

                    SortedSet<StartStop>.Enumerator v2 = v;
                    while (v2.MoveNext())
                    {
                        if (v2.Current.Start <= v.Current.Stop)
                        {
                            if (!v2.Current.Dead)
                            {
                                v2.Current.Dead = true;
                                v.Current.Stop = Math.Max(v.Current.Stop, v2.Current.Stop);
                                toRemove.Add(v2.Current);
                                Debug.WriteLine("rem from HStrokes @ {0}, from {1} to {2}", kvp.Key, v2.Current.Start, v2.Current.Stop);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                foreach (StartStop ssRemove in toRemove)
                {
                    kvp.Value.Remove(ssRemove);
                }
            }
            foreach (KeyValuePair<int, SortedSet<StartStop>> kvp in VStrokes)
            {
                if (kvp.Value.Count < 2)
                {
                    continue;
                }
                if (kvp.Key == 48)
                {
                    int stopper = 1;
                }
                List<StartStop> toRemove = new List<StartStop>();
                int strokesCount = kvp.Value.Count;
                for (int stroke = 0; stroke < strokesCount; stroke++)
                {
                    var v = kvp.Value.GetEnumerator();
                    int stroke2 = stroke + 1;
                    while (stroke2 > 0)
                    {
                        v.MoveNext();
                        stroke2--;
                    }

                    SortedSet<StartStop>.Enumerator v2 = v;
                    while (v2.MoveNext())
                    {
                        if (v2.Current.Start <= v.Current.Stop)
                        {
                            if (!v2.Current.Dead)
                            {
                                v2.Current.Dead = true;
                                v.Current.Stop = Math.Max(v.Current.Stop, v2.Current.Stop);
                                toRemove.Add(v2.Current);
                                Debug.WriteLine("rem from VStrokes @ {0}, from {1} to {2}", kvp.Key, v2.Current.Start, v2.Current.Stop);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                foreach (StartStop ssRemove in toRemove)
                {
                    kvp.Value.Remove(ssRemove);
                }
            }

            
            // now go through each VStroke and find any matching HStrokes
            foreach (KeyValuePair<int, SortedSet<StartStop>> kvp in VStrokes)
            {
                int xx = kvp.Key;
                SortedSet<StartStop> VStrokesAtX = kvp.Value;
                foreach (StartStop vss in VStrokesAtX)
                {
                    int startY = vss.Start;
                    int stopY = vss.Stop;
                    // find any hstrokes that are within the range of startY to stopY. How do we do this?
                    // how do we find the first hstroke at startY?
                    
                }

            }

            List<int> ll;
            SortedList<int, int> lll;
            
            
            // now none of the strokes should overlap.
            int stop = 0;
        }

        bool StrokesCross(int x, int startY, int stopY, int y, int startX, int stopX)
        {
            if (x < startX || x > stopX) return false;
            if (y < startY || y > stopY) return false;
            return true;
        }
    }

    internal class Program
    {

        static void Main(string[] args)
        {
            Random r = new Random(1);
            int n = 1000;
            char[] cAry = new char[n];
            int[] nLen = new int[n];
            char[] nDirecs = { 'U', 'D', 'L', 'R' };
            for (int i = 0; i < n; i++)
            {
                int rn = r.Next(0, 4);
                cAry[i] = nDirecs[rn];
                nLen[i] = r.Next(1, 100);
            }
            Compute c = new Compute();
            c.Go(cAry, nLen);
        }
    }
}