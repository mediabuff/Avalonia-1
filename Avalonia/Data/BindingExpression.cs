// -----------------------------------------------------------------------
// <copyright file="BindingExpression.cs" company="Steven Kirk">
// Copyright 2013 MIT Licence. See licence.md for more information.
// </copyright>
// -----------------------------------------------------------------------

namespace Avalonia.Data
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public class BindingExpression : BindingExpressionBase
    {
        private IPropertyPathParser pathParser;
        private PropertyPathToken[] chain;

        public BindingExpression(
                    IPropertyPathParser pathParser,
                    DependencyObject target,
                    DependencyProperty dp,
                    Binding binding)
                    : base(target, dp)
        {
            this.pathParser = pathParser;
            this.ParentBinding = binding;
        }

        public object DataItem
        {
            get { return this.ParentBinding.Source; }
        }

        public Binding ParentBinding
        {
            get;
            private set;
        }

        public object ResolvedSource
        {
            get;
            private set;
        }

        public string ResolvedSourcePropertyName
        {
            get;
            private set;
        }

        protected override object Evaluate()
        {
            Tuple<object, string> o = this.RewritePath(this.ParentBinding.Path.Path);
            this.chain = this.pathParser.Parse(o.Item1, o.Item2).ToArray();

            if (this.chain != null)
            {
                this.AttachListeners();

                PropertyPathToken last = this.chain.Last();

                if (last.Type == PropertyPathTokenType.FinalValue)
                {
                    return last.Object;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        private static FrameworkElement GetRoot(DependencyObject o)
        {
            DependencyObject parent;

            while ((parent = LogicalTreeHelper.GetParent(o)) != null)
            {
                o = LogicalTreeHelper.GetParent(parent);
            }

            return (FrameworkElement)o;
        }

        private Tuple<object, string> RewritePath(string path)
        {
            if (this.DataItem != null)
            {
                return Tuple.Create(this.DataItem, path);
            }
            else
            {
                if (this.ParentBinding.RelativeSource != null)
                {
                    switch (this.ParentBinding.RelativeSource.Mode)
                    {
                        case RelativeSourceMode.TemplatedParent:
                            FrameworkElement fe = this.Target as FrameworkElement;

                            if (fe != null)
                            {
                                return Tuple.Create((object)fe, "TemplatedParent." + path);
                            }
                            else
                            {
                                throw new InvalidOperationException("Cannot get TemplatedParent outside a Template.");
                            }
                    }
                }
            }

            throw new NotSupportedException("Don't know how to get binding source!");
        }

        private void AttachListeners()
        {
            foreach (PropertyPathToken link in this.chain.Take(this.chain.Length - 1))
            {
                this.AttachListener(link);
            }
        }

        private void AttachListener(PropertyPathToken link)
        {
            IObservableDependencyObject dependencyObject = link.Object as IObservableDependencyObject;

            if (dependencyObject != null)
            {
                dependencyObject.AttachPropertyChangedHandler(link.PropertyName, this.DependencyPropertyChanged);
            }
        }

        private void DetachListeners()
        {
            foreach (PropertyPathToken link in this.chain.Take(this.chain.Length - 1))
            {
                this.DetachListener(link);
            }
        }

        private void DetachListener(PropertyPathToken link)
        {
            IObservableDependencyObject dependencyObject = link.Object as IObservableDependencyObject;

            if (dependencyObject != null)
            {
                dependencyObject.RemovePropertyChangedHandler(link.PropertyName, this.DependencyPropertyChanged);
            }
        }

        private void DependencyPropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.DetachListeners();
            this.Invalidate();
        }
    }
}
