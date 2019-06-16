using System;
using System.Collections;
using System.Collections.Generic;
using Android.Views;

namespace CompositeAdapter
{
    public interface IUpdateable<TUpdateValue>
    {
        void Update(TUpdateValue value);
        TUpdateValue CurrentValue { get; }
    }
    
    public partial class CompositeAdapter
    {
        public delegate THolder HolderFactory<THolder>(View view);
        public delegate void SubscriptionFunction<THolder>(THolder holder);
        public delegate void UnsubscriptionFunction<THolder>(THolder holder);
        public delegate void ValueSubscriptionFunction<THolder, TValue>(THolder holder, Func<TValue> value);
        public delegate void BindingFunction<THolder, TValue>(THolder holder, TValue value);
        
        public interface ISingleViewBuilder
        {
            ISingleViewBuilder<THolder> Holding<THolder>(HolderFactory<THolder> holderFactory);
        }
        
        public interface IListBuilder<TItemValue>
        {
            IBindableBuilder<THolder, TItemValue, IList<TItemValue>> Holding<THolder>(
                HolderFactory<THolder> holderFactory,
                BindingFunction<THolder, TItemValue> bind);
        }
    
        public interface ISingleViewBuilder<THolder>
        {
            ISingleViewBuilder<THolder> WithSubscriptions(
                SubscriptionFunction<THolder> subscribe,
                UnsubscriptionFunction<THolder> unsubscribe);

            IBindableBuilder<THolder, T, T> Bindable<T>(BindingFunction<THolder, T> bind);
        }
        
        public interface IBindableBuilder<THolder, TItemValue, TUpdateValue> : IUpdateable<TUpdateValue>
        {
            IBindableBuilder<THolder, TItemValue, TUpdateValue> WithSubscriptions(
                ValueSubscriptionFunction<THolder, TItemValue> subscribe,
                UnsubscriptionFunction<THolder> unsubscribe);

            IUpdateable<TUpdateValue> AsUpdateable { get; }
        }

        private struct Untyped {}
        
        private class NodeBuilder<THolder, TItemValue, TUpdateValue> :
            IBindableBuilder<THolder, TItemValue, TUpdateValue>,
            ISingleViewBuilder,
            ISingleViewBuilder<THolder>,
            IListBuilder<TItemValue>
        {
            public readonly Node Node;

            private NodeBuilder(Node node)
            {
                Node = node;
            }

            public NodeBuilder(
                CompositeAdapter adapter, Func<LayoutInflater, ViewGroup, View> viewFactory, bool isSingleObject)
            {
                Node = new Node(adapter, viewFactory, isSingleObject);
            }

            ISingleViewBuilder<TNextHolder> ISingleViewBuilder.Holding<TNextHolder>(HolderFactory<TNextHolder> holderFactory)
            {
                Node.HolderFactory = v => holderFactory(v);
                return new NodeBuilder<TNextHolder, TItemValue, TUpdateValue>(Node);
            }

            ISingleViewBuilder<THolder> ISingleViewBuilder<THolder>.WithSubscriptions(SubscriptionFunction<THolder> subscribe, UnsubscriptionFunction<THolder> unsubscribe)
            {
                Node.Subscriber = (h, f) => subscribe((THolder) h);
                Node.Unsubscriber = h => unsubscribe((THolder) h);
                return new NodeBuilder<THolder, TItemValue, TUpdateValue>(Node);
            }

            IBindableBuilder<THolder, T, T> ISingleViewBuilder<THolder>.Bindable<T>(BindingFunction<THolder, T> bind)
            {
                Node.Binder = (h, v) => bind((THolder) h, (T) v);
                return new NodeBuilder<THolder, T, T>(Node);
            }

            IBindableBuilder<TNextHolder, TItemValue, IList<TItemValue>> IListBuilder<TItemValue>.Holding<TNextHolder>(HolderFactory<TNextHolder> holderFactory,
                BindingFunction<TNextHolder, TItemValue> bind)
            {
                Node.HolderFactory = v => holderFactory(v);
                Node.Binder = (h, v) => bind((TNextHolder) h, (TItemValue) v);
                return new NodeBuilder<TNextHolder, TItemValue, IList<TItemValue>>(Node);
            }

            IBindableBuilder<THolder, TItemValue, TUpdateValue> IBindableBuilder<THolder, TItemValue, TUpdateValue>.WithSubscriptions(ValueSubscriptionFunction<THolder, TItemValue> subscribe, UnsubscriptionFunction<THolder> unsubscribe)
            {
                Node.Subscriber = (h, f) => subscribe((THolder) h, () => (TItemValue) f());
                Node.Unsubscriber = h => unsubscribe((THolder) h);
                return new NodeBuilder<THolder, TItemValue, TUpdateValue>(Node);
            }

            void IUpdateable<TUpdateValue>.Update(TUpdateValue value)
            {
                if (Node.IsSingleObject)
                {
                    Node.Object = value;
                }
                else
                {
                    Node.Objects = (IList) value;
                }
                
                Node.Adapter.NodeChanged(Node);
                Node.RefreshListItemCount();
            }
            
            TUpdateValue IUpdateable<TUpdateValue>.CurrentValue => 
                Node.IsSingleObject ? (TUpdateValue) Node.Objects : (TUpdateValue) Node.Object;
            
            IUpdateable<TUpdateValue> IBindableBuilder<THolder, TItemValue, TUpdateValue>.AsUpdateable => this;
        }

        public ISingleViewBuilder WithView(View view)
        {
            var builder = new NodeBuilder<object, Untyped, Untyped>(
                this, (inflater, parent) =>
                {
                    (view.Parent as ViewGroup)?.RemoveView(view);
                    return view;
                }, true);

            AddNode(builder.Node);
            return builder;
        }
        
        public ISingleViewBuilder WithView(int resourceId)
        {
            var builder = new NodeBuilder<object, Untyped, Untyped>(
                this, (inflater, parent) => inflater.Inflate(resourceId, parent, false), true);

            AddNode(builder.Node);
            return builder;
        }
        
        public IListBuilder<T> WithList<T>(int resourceId)
        {
            var builder = new NodeBuilder<object, T, IList<T>>(
                this, (inflater, parent) => inflater.Inflate(resourceId, parent, false), false);

            AddNode(builder.Node);
            return builder;
        }
    }
}