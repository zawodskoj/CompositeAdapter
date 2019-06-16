using System;

namespace CompositeAdapter
{
    public class HandlerHolder
    {
        private Action _func;
        
        public EventHandler Set(Action func)
        {
            _func = func;
            return Handler;
        }
        
        public EventHandler Unset()
        {
            _func = null;
            return Handler;
        }

        private void Handler(object sender, EventArgs e)
        {
            _func?.Invoke();
        }
    }
}