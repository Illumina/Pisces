using System;
using System.Collections.Generic;
using Xunit;

namespace Pisces.Calculators.Tests
{
    public class SparseArrayTests : IDisposable
    {
        SparseArray<int> _array;

        public SparseArrayTests()
        {
            int[] arr = new int[] { 0, 0, 55, 50, 0, 22, 80, 0, 56, 52, 40, 0, 63 };
            _array = new SparseArray<int>();

            foreach (int val in arr)
                _array.Add(val);
        }

        public void Dispose()
        {
            _array = null;
        }

        [Fact]
        public void AddTest()
        {

            int count = _array.Count;
            _array.Add(0);
            Assert.Equal(count + 1, _array.Count);
            Assert.Equal(0, _array[_array.Count - 1]);

            _array.Add(5);
            Assert.Equal(count + 2, _array.Count);
            Assert.Equal(5, _array[_array.Count - 1]);
        }

        [Fact]
        public void ArgumentOutOfRangeTest()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _array[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _array[_array.Count+5]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _array[-1] = 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => _array[_array.Count + 5] = 3);
        }

        [Fact]
        public void ClearTest()
        {
            _array.Clear();
            Assert.Empty(_array);
        }

        [Fact]
        public void ContainsTest()
        {
#pragma warning disable xUnit2017 // Do not use Contains() to check if a value exists in a collection
            Assert.True(_array.Contains(56));
            Assert.False(_array.Contains(100));
#pragma warning restore xUnit2017 // Do not use Contains() to check if a value exists in a collection
        }

        [Fact]
        public void CopyToTest()
        {
            int[] arr = new int[_array.Count + 2];
            _array.CopyTo(arr, 2);
            Assert.Equal(_array[0], arr[2]);
            Assert.Equal(_array[5], arr[7]);
        }

        [Fact]
        public void EnumeratorTest()
        {
            IEnumerator<int> e = _array.GetEnumerator();

            for (int i = 0; i < _array.Count; i++)
            {
                Assert.True(e.MoveNext());
                Assert.Equal(_array[i], e.Current);
            }
        }
        [Fact]
        public void InsertTest()
        {
            SparseArray<int> test = new SparseArray<int>();
            test.Insert(0, 1);
            test.Insert(0, 0);
            test.Insert(1, 5);

            Assert.Equal(3, test.Count);
            Assert.Equal(0, test[0]);
            Assert.Equal(5, test[1]);
            Assert.Equal(1, test[2]);

            Assert.Throws<ArgumentOutOfRangeException>(() => test.Insert(5, 2));
        }

        [Fact]
        public void IndexOfTest()
        {
            SparseArray<int> test = new SparseArray<int>();

            test.Add(100);
            test.Add(200);

            Assert.Equal(1, test.IndexOf(200));
            Assert.Equal(-1, test.IndexOf(300));
        }

        [Fact]
        public void RemoveTest()
        {
            int count = _array.Count;
            bool isRemoved = _array.Remove(80);
            Assert.True(isRemoved);
            Assert.Equal(count - 1, _array.Count);
            Assert.DoesNotContain(80, _array);
            Assert.Equal(-1, _array.IndexOf(80));

            Assert.False(_array.Remove(100));
        }

        [Fact]
        public void RemoveAtTest()
        {
            int count = _array.Count;
            _array.RemoveAt(3);
            Assert.Equal(count - 1, _array.Count);
            Assert.Equal(0, _array[3]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _array.RemoveAt(count + 5));
        }

        [Fact]
        public void RemoveLastTest()
        {
            int count = _array.Count;
            _array.RemoveLast();
            Assert.Equal(count - 1, _array.Count);
            Assert.Equal(0, _array[_array.Count - 1]);

            _array.RemoveLast();
            Assert.Equal(count - 2, _array.Count);
            Assert.Equal(40, _array[_array.Count - 1]);
        }

    }
}
