using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using Android.Arch.Lifecycle;
using Android.Net.Wifi.Aware;
using Android.Support.V7.Util;
using Android.Support.V7.Widget;
using Android.Views;
using Object = Java.Lang.Object;

namespace CompositeAdapter
{
    public partial class CompositeAdapter : RecyclerView.Adapter
    {
        private class NodeHolder : RecyclerView.ViewHolder
        {
            public NodeHolder(Node node, object userHolder, View itemView) : base(itemView)
            {
                Node = node;
                UserHolder = userHolder;
            }

            public Node Node { get; }
            public object UserHolder { get; }
        }

        private class Node
        {
            private int _listItemCount;
            private IList _objects;

            public Node(CompositeAdapter adapter, Func<LayoutInflater, ViewGroup, View> viewFactory,
                bool isSingleObject)
            {
                Adapter = adapter;
                ViewFactory = viewFactory;
                IsSingleObject = isSingleObject;
            }

            public CompositeAdapter Adapter { get; }
            public Func<LayoutInflater, ViewGroup, View> ViewFactory { get; }
            public Func<View, object> HolderFactory { get; set; }
            public Action<object, object> Binder { get; set; }
            public Action<object, Func<object>> Subscriber { get; set; }
            public Action<object> Unsubscriber { get; set; }

            public bool IsSingleObject { get; set; }

            public IList OldObjects { get; private set; }
            public IList Objects
            {
                get => _objects;
                set
                {
                    OldObjects = _objects;
                    _objects = value;
                    if (_objects is INotifyCollectionChanged oncc)
                        oncc.CollectionChanged -= OnCollectionChanged;
                    if (value is INotifyCollectionChanged nncc)
                        nncc.CollectionChanged += OnCollectionChanged;
                }
            }

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                Adapter.NodeChanged(this, e);
            }

            public object Object { get; set; }

            public int ItemCount => IsSingleObject ? 1 : Objects?.Count ?? 0;
            public int RealCount => _listItemCount;
            
            public void CompleteListUpdate()
            {
                OldObjects = _objects;
                _listItemCount = Objects?.Count ?? 0;
            }
        }

        private readonly List<Node> _nodes = new List<Node>();

        private void AddNode(Node node)
        {
            _nodes.Add(node);
            NotifyDataSetChanged();
        }

        public override int ItemCount => _nodes.Sum(x => x.ItemCount); // todo: cache

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var cnt = 0;

            if (!(holder is NodeHolder nodeHolder)) throw new ArgumentException(nameof(holder));

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                
                if (cnt + node.ItemCount > position)
                {
                    var item = node.IsSingleObject ? node.Object : node.Objects[position - cnt];
                    node.Binder?.Invoke(nodeHolder.UserHolder, item);
                    return;
                }

                cnt += node.ItemCount;
            }

            throw new IndexOutOfRangeException(nameof(position));
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var node = _nodes[viewType - 1];
            var view = node.ViewFactory(LayoutInflater.FromContext(parent.Context), parent);

            return new NodeHolder(node, node.HolderFactory?.Invoke(view), view);
        }

        public override int GetItemViewType(int position)
        {
            var cnt = 0;

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                
                if (cnt + node.ItemCount > position)
                    return i + 1;

                cnt += node.ItemCount;
            }

            throw new IndexOutOfRangeException(nameof(position));
        }

        public override void OnViewAttachedToWindow(Object holder)
        {
            base.OnViewAttachedToWindow(holder);

            if (holder is NodeHolder nodeHolder)
            {
                nodeHolder.Node.Subscriber?.Invoke(
                    nodeHolder.UserHolder,
                    nodeHolder.Node.IsSingleObject 
                        ? (Func<object>) (() => nodeHolder.Node.Object)
                        : () => throw new NotImplementedException()); // TODO
            }
        }

        public override void OnViewDetachedFromWindow(Object holder)
        {
            if (holder is NodeHolder nodeHolder)
            {
                nodeHolder.Node.Unsubscriber?.Invoke(nodeHolder.UserHolder);
            }
            
            base.OnViewDetachedFromWindow(holder);
        }

        private class DiffUtilCallback : DiffUtil.Callback
        {
            private readonly IList _oldObjects;
            private readonly IList _objects;
            private static readonly IList EmptyList = Array.Empty<object>();
            
            public DiffUtilCallback(IList oldObjects, IList objects)
            {
                _oldObjects = oldObjects ?? EmptyList;
                _objects = objects ?? EmptyList;
            }

            public override bool AreContentsTheSame(int oldItemPosition, int newItemPosition)
            {
                var newObj = _objects[newItemPosition];
                var oldObj = _oldObjects[newItemPosition];

                return Equals(newObj, oldObj);
            }

            public override bool AreItemsTheSame(int oldItemPosition, int newItemPosition)
            {
                var newObj = _objects[newItemPosition];
                var oldObj = _oldObjects[newItemPosition];

                return ReferenceEquals(newObj, oldObj);
            }

            public override int NewListSize => _objects.Count;
            public override int OldListSize => _oldObjects.Count;
        }

        private void NodeChanged(Node node, NotifyCollectionChangedEventArgs changeArgs = null)
        {
            var count = 0;
            
            foreach (var n in _nodes)
            {
                if (n == node)
                {
                    if (node.IsSingleObject)
                    {
                        NotifyItemChanged(count);
                    }
                    else
                    {
                        if (changeArgs != null)
                        {
                            // we have actual changes

                            switch (changeArgs.Action)
                            {
                                case NotifyCollectionChangedAction.Add:
                                    NotifyItemRangeInserted(count + changeArgs.NewStartingIndex, changeArgs.NewItems.Count);
                                    break;
                                case NotifyCollectionChangedAction.Move:
                                    var movedCount = changeArgs.NewItems.Count;

                                    var sig = Math.Sign(changeArgs.OldStartingIndex - changeArgs.NewStartingIndex);
                                    if (sig == 0) break;
                                    
                                    var startNew = sig > 0
                                        ? changeArgs.NewStartingIndex
                                        : changeArgs.NewStartingIndex + movedCount - 1;
                                    var startOld = sig > 0
                                        ? changeArgs.OldStartingIndex
                                        : changeArgs.OldStartingIndex + movedCount - 1;

                                    for (var cur = 0; cur < movedCount; cur++)
                                    {
                                        NotifyItemMoved(startOld + cur * sig, startNew + cur * sig);
                                    }

                                    break;
                                case NotifyCollectionChangedAction.Remove:
                                    NotifyItemRangeRemoved(count + changeArgs.OldStartingIndex, changeArgs.OldItems.Count);
                                    break;
                                case NotifyCollectionChangedAction.Replace:
                                    if (changeArgs.NewStartingIndex == -1)
                                        throw new InvalidOperationException("Unsupported NotifyCollectionChanged operation");

                                    var diff = changeArgs.NewItems.Count - changeArgs.OldItems.Count;
                                    if (diff > 0)
                                    {
                                        NotifyItemRangeChanged(count, changeArgs.NewStartingIndex);
                                        NotifyItemRangeInserted(count + changeArgs.NewStartingIndex, diff);
                                    }
                                    else if (diff < 0)
                                    {
                                        NotifyItemRangeChanged(count, changeArgs.NewStartingIndex);
                                        NotifyItemRangeRemoved(count + changeArgs.NewStartingIndex, -diff);
                                    }
                                    else
                                    {
                                        NotifyItemRangeChanged(count, changeArgs.NewStartingIndex);
                                    }
                                    break;
                                case NotifyCollectionChangedAction.Reset:
                                    Reset();
                                    break;
                            }
                        }
                        else if (!ReferenceEquals(node.Objects, node.OldObjects))
                        {
                            // we can diff

                            var diff = DiffUtil.CalculateDiff(new DiffUtilCallback(node.OldObjects, node.Objects));
                            diff.DispatchUpdatesTo(this);
                        }
                        else
                        {
                            Reset();
                        }

                        void Reset()
                        {
                            var prevCount = node.RealCount;
                            var newCount = node.Objects?.Count ?? 0;
                            var diff = newCount - prevCount;
                            if (diff > 0)
                            {
                                NotifyItemRangeChanged(count, prevCount);
                                NotifyItemRangeInserted(count + prevCount, diff);
                            }
                            else if (diff < 0)
                            {
                                NotifyItemRangeChanged(count, newCount);
                                NotifyItemRangeRemoved(count + newCount, -diff);
                            }
                            else
                            {
                                NotifyItemRangeChanged(count, newCount);
                            }
                        }
                    }
                }
                else
                {
                    count += n.ItemCount;
                }
            }
        }
    }
}