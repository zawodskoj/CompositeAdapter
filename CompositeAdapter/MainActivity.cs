﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Widget;

namespace CompositeAdapter
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private int _count;
        private readonly ObservableCollection<string> _strings = new ObservableCollection<string>(Enumerable.Range(1, 5).Select(x => x.ToString()));
        private IUpdateable<string> _header;
        private IUpdateable<IList<string>> _list;
        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            var rv = FindViewById<RecyclerView>(Resource.Id.rv);
            rv.SetLayoutManager(new LinearLayoutManager(this));
            rv.AddItemDecoration(new DividerItemDecoration(this, RecyclerView.Vertical));

            var adapter = new CompositeAdapter
            {
                HasStableIds = true // do not use if they are not really stable
            };

            _header = adapter.WithView(Resource.Layout.header1)
                .Holding(x => new {text = (TextView) x})
                .Bindable<string>((x, v) => x.text.Text = v)
                .AsUpdateable;

            adapter.WithView(Resource.Layout.header2)
                .Holding(x => new
                {
                    btn = (Button) x,
                    click = new HandlerHolder()
                })
                .WithSubscriptions(
                    x => x.btn.Click += x.click.Set(Increase),
                    x => x.btn.Click -= x.click.Unset());

            _list = adapter.WithList<string>(Resource.Layout.text_item)
                .Holding(
                    x => new {text = x.FindViewById<TextView>(Resource.Id.text)},
                    (x, v) => x.text.Text = v)
                .WithId(x => x.GetHashCode()) // kek puk
                .AsUpdateable;
                
            adapter.WithView(Resource.Layout.footer);
            
            Increase();
            rv.SetAdapter(adapter);
        }

        private void Increase()
        {
            _strings.Insert(0, "New " + _count);
            _count++;
            _header.Update("HEADER - " + _count);
        }
    }
}