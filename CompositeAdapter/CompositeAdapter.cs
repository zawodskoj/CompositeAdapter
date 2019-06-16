using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using Android.Arch.Lifecycle;
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

            public IList Objects { get; set; }
            public object Object { get; set; }

            public int ItemCount => IsSingleObject ? 1 : _listItemCount;
            
            public void RefreshListItemCount()
            {
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

        private void NodeChanged(Node node)
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
                        // todo: real diff
                        
                        var prevCount = node.ItemCount;
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
                else
                {
                    count += n.ItemCount;
                }
            }
        }
    }
}