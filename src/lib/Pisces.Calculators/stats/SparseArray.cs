using System;
using System.Collections;
using System.Collections.Generic;

namespace Pisces.Calculators
{
    /// <summary>
    /// This is a collection for storing "sparse" structures as a list, as in, many of the values in the list are 0.
    /// This saves memory by using a counter to keep track of the number of zeros in the list and uses a dictionary
    /// to store non-zero values.  Retrieval from the array should therefore still be in constant time.
    /// </summary>
    /// 
    /// <typeparam name="T">Must be numeric</typeparam>
    /// 
    /// <remarks>Author: Sidney Kuo</remarks>
    public class SparseArray<T> : IList<T> where T :
            struct,
            IComparable,
            IComparable<T>,
            IConvertible,
            IEquatable<T>,
            IFormattable
    {
        #region Fields
        private Dictionary<int, T> data = new Dictionary<int, T>();
        private int zeros = 0;
        #endregion

        #region Properties
        public int Count { get; private set; } = 0;
        public bool IsReadOnly { get; } = false;
        #endregion

        #region Indexer
        public T this[int i]
        {
            get
            {
                if (i > Count - 1 || i < 0)
                    throw new ArgumentOutOfRangeException();
                else if (!data.ContainsKey(i))
                    return (T)Convert.ChangeType(0, typeof(T));
                else
                    return data[i];
            }

            set
            {
                if (i > Count - 1 || i < 0)
                    throw new ArgumentOutOfRangeException();
                else if (Convert.ToDouble(value) == 0)
                {
                    if (data.ContainsKey(i))
                    {
                        zeros++;
                        data.Remove(i);
                    }
                    //else zeros++ and zeros--
                }
                else
                    data[i] = value;
            }
        }
        #endregion

        #region Constructor
        public SparseArray() { }
        public SparseArray(int size) {
            Count = size;
            zeros = size;
        }
        #endregion

        #region Methods
        public void Add(T element)
        {
            if (Convert.ToDouble(element) == 0)
                zeros++;
            else            
                data.Add(Count, element);
            
            Count++;
        }

        public void Clear()
        {
            Count = 0;
            zeros = 0;
            data.Clear();
        }

        public bool Contains(T item)
        {
            if (Convert.ToDouble(item) == 0 && zeros > 0)
                return true;
            else if (Convert.ToDouble(item) == 0 && zeros < 1)
                return false;
            else            
                return data.ContainsValue(item);            
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            for (int i = 0; i < Count; i++)
            {
                array[arrayIndex] = this[i];
                arrayIndex++;
            }
        }

        public int IndexOf(T item)
        {
            for (int i =0; i < Count; i++)
            {
                if (this[i].CompareTo(item) == 0)
                    return i;
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            if (index > Count)
                throw new ArgumentOutOfRangeException();
            else if (index == Count)
                Add(item);
            else
            {
                Add(this[Count - 1]); // Count is now incremented by 1

                for (int i = Count - 2; i > index; i--)
                    this[i] = this[i - 1];

                this[index] = item;
            }
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);

            if (index == -1)
                return false;
            else
                RemoveAt(index);

            return true;
        }

        public void RemoveAt(int index)
        {
            if (index > Count - 1)
                throw new ArgumentOutOfRangeException();

            for (int i = index; i < Count - 1; i++)           
                this[i] = this[i + 1];

            RemoveLast();
        }

        public void RemoveLast()
        {
            if (Convert.ToDouble(this[Count-1]) == 0)
                zeros--;
            else
                data.Remove(Count-1);

            Count--;
        }

        public void Reorder()
        {
            Dictionary<int, T> newData = new Dictionary<int, T>();
            int newIndex = 0;
            foreach (var item in data)
            {
                newData.Add(newIndex, item.Value);
                newIndex++;
            }

            data = newData;
        }
        #endregion

        #region IEnumerable methods
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }
}
