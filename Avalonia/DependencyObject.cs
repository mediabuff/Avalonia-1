﻿//
// DependencyObject.cs
//
// Author:
//   Iain McCoy (iain@mccoy.id.au)
//   Chris Toshok (toshok@ximian.com)
//
// (C) 2005 Iain McCoy
// (C) 2007 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

namespace Avalonia
{
    using System;
    using System.Collections.Generic;
    using Avalonia.Data;
    using Avalonia.Threading;

    public class DependencyObject : DispatcherObject, IObservableDependencyObject
    {
        private static Dictionary<Type, Dictionary<string, DependencyProperty>> propertyDeclarations = 
            new Dictionary<Type, Dictionary<string, DependencyProperty>>();

        private Dictionary<DependencyProperty, object> properties = 
            new Dictionary<DependencyProperty, object>();

        private Dictionary<DependencyProperty, BindingExpressionBase> propertyExpressions =
            new Dictionary<DependencyProperty, BindingExpressionBase>();

        private Dictionary<string, List<DependencyPropertyChangedEventHandler>> propertyChangedHandlers =
            new Dictionary<string, List<DependencyPropertyChangedEventHandler>>();

        public bool IsSealed
        {
            get { return false; }
        }

        public DependencyObjectType DependencyObjectType
        {
            get { return DependencyObjectType.FromSystemType(GetType()); }
        }

        public void ClearValue(DependencyProperty dp)
        {
            if (IsSealed)
                throw new InvalidOperationException("Cannot manipulate property values on a sealed DependencyObject");

            properties[dp] = null;
        }

        public void ClearValue(DependencyPropertyKey key)
        {
            ClearValue(key.DependencyProperty);
        }

        public void CoerceValue(DependencyProperty dp)
        {
            PropertyMetadata pm = dp.GetMetadata(this);
            if (pm.CoerceValueCallback != null)
                pm.CoerceValueCallback(this, GetValue(dp));
        }

        public sealed override bool Equals(object obj)
        {
            throw new NotImplementedException("Equals");
        }

        public sealed override int GetHashCode()
        {
            throw new NotImplementedException("GetHashCode");
        }

        public LocalValueEnumerator GetLocalValueEnumerator()
        {
            return new LocalValueEnumerator(properties);
        }

        public object GetValue(DependencyProperty dp)
        {
            object val;

            if (!this.properties.TryGetValue(dp, out val))
            {
                val = dp.DefaultMetadata.DefaultValue;
                this.properties[dp] = val;
            }

            return val;
        }

        public void InvalidateProperty(DependencyProperty dp)
        {
            BindingExpressionBase binding;
            object value = this.GetValue(dp);

            if (this.propertyExpressions.TryGetValue(dp, out binding))
            {
                object oldValue = value;

                value = binding.GetCurrentValue();
                
                this.properties[dp] = value;
                this.OnPropertyChanged(new DependencyPropertyChangedEventArgs(
                    dp, 
                    oldValue, 
                    value));
            }

        }

        void IObservableDependencyObject.AttachPropertyChangedHandler(
            string propertyName, 
            DependencyPropertyChangedEventHandler handler)
        {
            List<DependencyPropertyChangedEventHandler> handlers;

            if (!this.propertyChangedHandlers.TryGetValue(propertyName, out handlers))
            {
                handlers = new List<DependencyPropertyChangedEventHandler>();
                this.propertyChangedHandlers.Add(propertyName, handlers);
            }

            handlers.Add(handler);
        }

        void IObservableDependencyObject.RemovePropertyChangedHandler(
            string propertyName, 
            DependencyPropertyChangedEventHandler handler)
        {
            List<DependencyPropertyChangedEventHandler> handlers;

            if (this.propertyChangedHandlers.TryGetValue(propertyName, out handlers))
            {
                handlers.Remove(handler);
            }
        }

        protected virtual void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            PropertyMetadata pm = e.Property.GetMetadata(this);

            if (pm != null)
            {
                if (pm.PropertyChangedCallback != null)
                    pm.PropertyChangedCallback(this, e);
            }

            List<DependencyPropertyChangedEventHandler> handlers;

            if (this.propertyChangedHandlers.TryGetValue(e.Property.Name, out handlers))
            {
                foreach (var handler in handlers.ToArray())
                {
                    handler(this, e);
                }
            }
        }

        public object ReadLocalValue(DependencyProperty dp)
        {
            object val = properties[dp];
            return val == null ? DependencyProperty.UnsetValue : val;
        }

        public void SetValue(DependencyProperty dp, object value)
        {
            if (IsSealed)
                throw new InvalidOperationException("Cannot manipulate property values on a sealed DependencyObject");

            if (!(value is Expression) && !dp.IsValidType(value))
                throw new ArgumentException("value not of the correct type for this DependencyProperty");

            ValidateValueCallback validate = dp.ValidateValueCallback;

            if (validate != null && !validate(value))
            {
                throw new Exception("Value does not validate");
            }
            else
            {
                object oldValue;
                BindingExpressionBase binding = value as BindingExpressionBase;

                this.properties.TryGetValue(dp, out oldValue);

                if (binding != null)
                {
                    this.propertyExpressions.Add(dp, binding);
                    value = binding.GetCurrentValue();
                }
                else
                {
                    this.propertyExpressions.Remove(dp);
                }

                properties[dp] = value;

                this.OnPropertyChanged(new DependencyPropertyChangedEventArgs(dp, oldValue, value));
            }
        }

        public void SetValue(DependencyPropertyKey key, object value)
        {
            SetValue(key.DependencyProperty, value);
        }

        protected virtual bool ShouldSerializeProperty(DependencyProperty dp)
        {
            throw new NotImplementedException();
        }

        internal static void Register(Type t, DependencyProperty dp)
        {
            if (!propertyDeclarations.ContainsKey(t))
                propertyDeclarations[t] = new Dictionary<string, DependencyProperty>();
            Dictionary<string, DependencyProperty> typeDeclarations = propertyDeclarations[t];
            if (!typeDeclarations.ContainsKey(dp.Name))
                typeDeclarations[dp.Name] = dp;
            else
                throw new ArgumentException("A property named " + dp.Name + " already exists on " + t.Name);
        }
    }
}