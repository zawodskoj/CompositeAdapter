using Android.App;
using Android.Views;

namespace RvTest
{
    public static class ViewGroupExtensions
    {
        public static View ExtractView(this ViewGroup group, int resourceId)
        {
            var view = group.FindViewById(resourceId);
            if (view == null) return null;

            ((ViewGroup)view.Parent).RemoveView(view);
            return view;
        }

        public static T ExtractView<T>(this ViewGroup group, int resourceId) where T : View
        {
            var view = group.FindViewById<T>(resourceId);
            if (view == null) return null;

            ((ViewGroup)view.Parent).RemoveView(view);
            return view;
        }

        public static View ExtractView(this Activity activity, int resourceId)
        {
            var view = activity.FindViewById(resourceId);
            if (view == null) return null;

            ((ViewGroup)view.Parent).RemoveView(view);
            return view;
        }

        public static T ExtractView<T>(this Activity activity, int resourceId) where T : View
        {
            var view = activity.FindViewById<T>(resourceId);
            if (view == null) return null;

            ((ViewGroup)view.Parent).RemoveView(view);
            return view;
        }
    }
}