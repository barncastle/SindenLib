using System;
using System.Collections;
using System.Collections.Generic;

namespace SindenLib.Models
{
    class CircularBuffer<T> : IEnumerable<T>
    {
        private readonly Queue<T> Buffer;
        private readonly int Capacity;

        public CircularBuffer(int capacity, params T[] values)
        {
            Buffer = new Queue<T>(capacity);
            Capacity = capacity;

            if (values?.Length > 0)
                Array.ForEach(values, Add);
        }

        public void Add(T value)
        {
            if (Buffer.Count == Capacity)
                Buffer.Dequeue();

            Buffer.Enqueue(value);
        }

        public IEnumerator<T> GetEnumerator() => Buffer.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
