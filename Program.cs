using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Runtime.Intrinsics;

namespace ScrappyCoco
{
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

    class HStrokePlus : IComparable<HStrokePlus>
    {
        public int Y;
        public List<StartStop>? HStrokeList;

        public int CompareTo(HStrokePlus? other)
        {
            return this.Y - other.Y;
        }

        public HStrokePlus( int y, SortedSet<StartStop> strokes )
        {
            Y = y;
            if (strokes != null)
            {
                HStrokeList = new List<StartStop>();
                foreach (StartStop ss in strokes)
                {
                    HStrokeList.Add(ss);
                }
            }
        }

        public StartStop? FindStrokeCrossesX(int x)
        {
            if( HStrokeList == null )
            {
                return null;
            }

            if( HStrokeList.Count == 0 )
            {
                return null;
            }

            int l = 0;
            int h = HStrokeList.Count - 1;
            while (l <= h)
            {
                int m = (l + h) / 2;
                StartStop ss = HStrokeList[m];
                if (x < ss.Start)
                {
                    h = m - 1;
                    continue;
                }
                if( x > ss.Stop )
                {
                    l = m + 1;
                    continue;
                }
                return ss;
            }

            return null;
        }
    }

    class Compute
    {
        Dictionary<int, SortedSet<StartStop>> VStrokes = new Dictionary<int, SortedSet<StartStop>>();
        Dictionary<int, SortedSet<StartStop>> HStrokes = new Dictionary<int, SortedSet<StartStop>>();
        SortedSet<HStrokePlus> HStrokesSorted = new SortedSet<HStrokePlus>();
        List<Point> FoundCrosses = new List<Point>();

        public void Go(char[] dirs, int [] lens)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

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
                            int final = y + len;
                            bool b = VStrokes.TryGetValue(x, out SortedSet<StartStop> vss);
                            if (!b)
                            {
                                vss = new SortedSet<StartStop>();
                                VStrokes[x] = vss;
                            }
                            StartStop ssAdd = new StartStop(y, final);
                            vss.TryGetValue(ssAdd, out StartStop ssAlready);
                            if (ssAlready != null)
                            {
                                ssAlready.Stop = Math.Max(ssAlready.Stop, final);
                            }
                            else
                            {
                                bool bAdded = vss.Add(ssAdd);
                                Debug.Assert( bAdded );
                            }
                            y = final;
                            break;
                        }
                    case 'D':
                        {
                            int final = y - len;
                            bool b = VStrokes.TryGetValue(x, out SortedSet<StartStop> vss);
                            if (!b)
                            {
                                vss = new SortedSet<StartStop>();
                                VStrokes[x] = vss;
                            }
                            StartStop ssAdd = new StartStop(final, y);
                            vss.TryGetValue(ssAdd, out StartStop ssAlready);
                            if (ssAlready != null)
                            {
                                ssAlready.Stop = Math.Max(ssAlready.Stop, y);
                            }
                            else
                            {
                                bool bAdded = vss.Add(ssAdd);
                                Debug.Assert(bAdded);
                            }
                            y = final;
                            break;
                        }
                    case 'L':
                        {
                            int final = x - len;
                            bool b = HStrokes.TryGetValue(y, out SortedSet<StartStop> hss);
                            if (!b)
                            {
                                hss = new SortedSet<StartStop>();
                                HStrokes[y] = hss;
                            }
                            StartStop ssAdd = new StartStop(final, x);
                            hss.TryGetValue(ssAdd, out StartStop ssAlready);
                            if (ssAlready != null)
                            {
                                ssAlready.Stop = Math.Max(ssAlready.Stop, x);
                            }
                            else
                            {
                                bool bAdded = hss.Add(ssAdd);
                                Debug.Assert(bAdded);
                            }
                            x = final;
                            break;
                        }
                    case 'R':
                        {
                            int final = x + len;
                            bool b = HStrokes.TryGetValue(y, out SortedSet<StartStop> hss);
                            if (!b)
                            {
                                hss = new SortedSet<StartStop>();
                                HStrokes[y] = hss;
                            }
                            StartStop ssAdd = new StartStop(x, final);
                            hss.TryGetValue(ssAdd, out StartStop ssAlready);
                            if (ssAlready != null)
                            {
                                ssAlready.Stop = Math.Max(ssAlready.Stop, final);
                            }
                            else
                            {
                                bool bAdded = hss.Add(ssAdd);
                                Debug.Assert(bAdded);
                            }
                            x = final;
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
                                // Debug.WriteLine("rem from HStrokes @ {0}, from {1} to {2}", kvp.Key, v2.Current.Start, v2.Current.Stop);
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
                                // Debug.WriteLine("rem from VStrokes @ {0}, from {1} to {2}", kvp.Key, v2.Current.Start, v2.Current.Stop);
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

            foreach( KeyValuePair<int, SortedSet<StartStop>> kvp in HStrokes)
            {
                HStrokesSorted.Add(new HStrokePlus(kvp.Key, kvp.Value));
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
                    SortedSet<HStrokePlus> hss = HStrokesSorted.GetViewBetween(new HStrokePlus(startY, null), new HStrokePlus(stopY, null));

                    // need a quick way to find if any of the strokes in hss.HStrokes cross our xx
                    // this seems to imply a specialized binary search

                    foreach( HStrokePlus hsp in hss )
                    {
                        // quickly find a stroke, if any, that crosses X
                        StartStop? ssFound = hsp.FindStrokeCrossesX(xx);
                        if (ssFound != null)
                        {
                            FoundCrosses.Add(new Point(xx, hsp.Y));
                        }
                    }
                    
                }

            }

            stopwatch.Stop();
            long seconds = stopwatch.ElapsedMilliseconds / 1000;
            Console.WriteLine("For {2}, took {0} seconds, found {1} crosses", seconds, FoundCrosses.Count, dirs.Length);
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
            char[] nDirecs = { 'U', 'D', 'L', 'R' };

            for (int count = 1024; count < 1000000000; count *= 2)
            {
                int n = count;
                char[] cAry = new char[n];
                int[] nLen = new int[n];
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
}