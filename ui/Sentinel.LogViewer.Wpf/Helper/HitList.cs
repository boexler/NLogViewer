using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The hits of the search as a list that the Results tab shows: it reads the list of the viewer, so a list of many hits is not copied, and it
    /// says <see cref="CollectionChanged"/> when the viewer has made a new one
    /// </summary>
    internal sealed class HitList : IList, INotifyCollectionChanged
    {
        private readonly List<LogEntry> _hits;

        public HitList(List<LogEntry> hits)
        {
            _hits = hits;
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public void Changed() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        public int Count => _hits.Count;

        public bool IsReadOnly => true;

        public bool IsFixedSize => true;

        public bool IsSynchronized => false;

        public object SyncRoot => _hits;

        public object? this[int index]
        {
            get => _hits[index];
            set => throw new NotSupportedException();
        }

        public bool Contains(object? value) => value is LogEntry entry && _hits.Contains(entry);

        public int IndexOf(object? value) => value is LogEntry entry ? _hits.IndexOf(entry) : -1;

        public void CopyTo(Array array, int index) => ((ICollection)_hits).CopyTo(array, index);

        public IEnumerator GetEnumerator() => _hits.GetEnumerator();

        public int Add(object? value) => throw new NotSupportedException();

        public void Insert(int index, object? value) => throw new NotSupportedException();

        public void Remove(object? value) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();
    }
}
