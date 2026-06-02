using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Core.Tools
{
    public class RingHistoryEnumerator<T> : IEnumerator<T>
    {
        public RingHistoryEnumerator(ref T[] data, ref float[] historyTimes, int firstItemIndex, int length, float oldestValidTime, bool logSkipedEntries = false)
        {
            this.data = data;
            this.historyTimes = historyTimes;
            if (data.Length != historyTimes.Length || length > historyTimes.Length)
            {
                throw new InvalidOperationException();
            }

            int firstValidEntry = firstItemIndex;

            int validLength = length;
            // Find the first young enough entry
            int maxEntrySkipped = -1;
            for (int i = 0; i < length; i++)
            {
                int index = (firstItemIndex + i) % historyTimes.Length;
                if (historyTimes[index] < oldestValidTime)
                {
                    // Entry is too old: won't be enumerated
                    maxEntrySkipped = index;
                    firstValidEntry = (index + 1) % historyTimes.Length;
                    validLength--;
                }
                else
                {
                    break;
                }
            }

            if (logSkipedEntries && maxEntrySkipped >= 0)
            {
                int skippedEntries = (maxEntrySkipped - firstItemIndex + 1 + historyTimes.Length) % historyTimes.Length;
                // Handle the case where all entries are skipped as too old
                if (skippedEntries == 0) skippedEntries = historyTimes.Length;
                Debug.LogError($"Some entries ({firstItemIndex}<->{maxEntrySkipped}, {skippedEntries}/{length}) too old:" +
                    $" won't be enumerated, for times {historyTimes[firstItemIndex]} <-> {historyTimes[maxEntrySkipped]} / Oldest time accepted: {oldestValidTime}");
            }
            this.length = validLength;
            this.currentPos = firstValidEntry - 1;
        }

        int length;
        T[] data;
        float[] historyTimes;

        int currentPos = -1;
        int moves = 0;

        public T CurrentData
        {
            get
            {
                try
                {
                    return data[currentPos];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        #region IEnumerator
        object IEnumerator.Current => CurrentData;

        T IEnumerator<T>.Current => CurrentData;

        bool IEnumerator.MoveNext()
        {
            currentPos = (currentPos + 1) % historyTimes.Length;
            moves++;
            bool canMove = length > 0 && moves <= length;
            return canMove;
        }

        void IEnumerator.Reset()
        {
            currentPos = -1;
            moves = 0;
        }

        void IDisposable.Dispose()
        {
        }
        #endregion
    }

    // Ring buffer history
    public class RingHistory<T> : IEnumerable<T>
    {
        T[] historyData;
        float[] historyTimes;
        public int nextHistoryEntry = 0;
        public int firstHistoryEntry = -1;
        public float oldestValidTime = 0;
        public int size = 0;

        public RingHistory(int size)
        {
            this.size = size;
            historyData = new T[size];
            historyTimes = new float[size];
            for (int i = 0; i < size; i++)
            {
                historyTimes[i] = -1;
            }
        }

        public void Add(T data, float time)
        {
            // 2 initial cases: buffer never filled once (firstIndex == 0), and buffer filled once (firstIndex == nextIndex)
            bool bufferFilledOnce = firstHistoryEntry == nextHistoryEntry;

            historyData[nextHistoryEntry] = data;
            historyTimes[nextHistoryEntry] = time;

            nextHistoryEntry = (nextHistoryEntry + 1) % historyData.Length;

            if ((1 + nextHistoryEntry) > historyData.Length)
            {
                // Data will go other the max index
                bufferFilledOnce = true;
            }

            if (bufferFilledOnce)
            {
                firstHistoryEntry = nextHistoryEntry;
            }
            else if (firstHistoryEntry == -1)
            {
                // First write: setting firstIndex to 0
                firstHistoryEntry = 0;
            }
        }

        // Note: suppose all are added with an increasing time compared to the last addition
        public int Count { 
            get
            {
                int firstValidEntry = firstHistoryEntry;
                int length = CountWithoutTimeCheck;
                int validLength = length;
                // Find the first young enough entry
                int maxEntrySkipped = -1;
                for (int i = 0; i < length; i++)
                {
                    int index = (firstHistoryEntry + i) % historyTimes.Length;
                    if (historyTimes[index] < oldestValidTime)
                    {
                        // Entry is too old: won't be enumerated
                        maxEntrySkipped = index;
                        firstValidEntry = (index + 1) % historyTimes.Length;
                        validLength--;
                    }
                    else
                    {
                        break;
                    }
                }
                return validLength;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new RingHistoryEnumerator<T>(ref historyData, ref historyTimes, firstHistoryEntry, CountWithoutTimeCheck, oldestValidTime);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Ignore age
        public int CountWithoutTimeCheck
        {
            get
            {
                if (firstHistoryEntry == -1) return 0;
                if (firstHistoryEntry != nextHistoryEntry)
                {
                    return nextHistoryEntry;
                }
                return historyData.Length;
            }
        }

        public bool TryGetLast(out T last)
        {
            last = default;
            if (historyData.Length > 0 && CountWithoutTimeCheck > 0)
            {
                var lastIndex = (nextHistoryEntry + historyData.Length - 1) % historyData.Length;
                if (historyTimes[lastIndex] >= oldestValidTime)
                {
                    last = historyData[lastIndex];
                    return true;
                }
            }
            return false;
        }
        /*
        public T FirstWithoutTimeCheck
        {
            get
            {
                if (firstHistoryEntry == -1) throw new ArgumentOutOfRangeException();
                return historyData[firstHistoryEntry];
            }
        }
        */
    }

    public static class RingHistoryTest
    {
        public static void TestRH()
        {
            var history = new RingHistory<int>(5);
            int last = 0;
            for(int i = 1; i <= 9; i++)
            {
                Debug.LogError($"----- {history.oldestValidTime} [ {history.firstHistoryEntry} - {history.nextHistoryEntry}] ({history.Count}/{history.CountWithoutTimeCheck}) --"); 
                foreach (var h in history) Debug.LogError($"- {h}"); 
                if (history.TryGetLast(out last)) Debug.LogError($" => last {last}");
                history.Add(i, i);
            }
            history.oldestValidTime = 7;
            Debug.LogError($"----- {history.oldestValidTime} [ {history.firstHistoryEntry} - {history.nextHistoryEntry}] ({history.Count}/{history.CountWithoutTimeCheck}) --"); foreach (var h in history) Debug.LogError($"- {h}"); if (history.TryGetLast(out last)) Debug.LogError($" => last {last}");
            history.oldestValidTime = 25;
            Debug.LogError($"----- {history.oldestValidTime} [ {history.firstHistoryEntry} - {history.nextHistoryEntry}] ({history.Count}/{history.CountWithoutTimeCheck}) --"); foreach (var h in history) Debug.LogError($"- {h}"); if (history.TryGetLast(out last)) Debug.LogError($" => last {last}");
        }
    }
}
