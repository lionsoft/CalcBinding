﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Linq.Expressions;
using DynamicExpresso;
using CalcBinding.Inverse;
using System.Diagnostics;

namespace CalcBinding
{
    /// <summary>
    /// Converter that supports expression evaluate
    /// </summary>
    public class CalcConverter : IValueConverter, IMultiValueConverter
    {
        private IExpressionParser parser;
        private Lambda compiledExpression;
        private Lambda compiledInversedExpression;

        public bool StringFormatDefined { get; set; }

        private FalseToVisibility falseToVisibility = CalcBinding.FalseToVisibility.Collapsed;
        public FalseToVisibility FalseToVisibility 
        {
            get { return falseToVisibility; }
            set { falseToVisibility = value; }
        }

        #region Init

        public CalcConverter() : this(new InterpreterParser()) { }

        public CalcConverter(IExpressionParser parser)
        {
            this.parser = parser;        
        }

        public CalcConverter(FalseToVisibility falseToVisibility)
        {
            FalseToVisibility = falseToVisibility;
        }

        public CalcConverter(FalseToVisibility falseToVisibility, IExpressionParser parser)
        {
            FalseToVisibility = falseToVisibility;
            this.parser = parser;
        } 

        #endregion

        #region IValueConverter
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(new [] { value }, targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (compiledExpression == null)
            {
                if ((compiledExpression = compileExpression(null, (string)parameter, true, new List<Type>{targetType})) == null)
                    return null;
            }

            if (compiledInversedExpression == null)
            {
                //try convert back expression
                try
                {
                    var resType = compiledExpression.Expression.Type;
                    var param = System.Linq.Expressions.Expression.Parameter(resType, "Path");
                    compiledInversedExpression = new Inverse.Inverter(parser).InverseExpression(compiledExpression.Expression, param);
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Binding error: calc converter can't convert back expression " + parameter + ": " + e.Message);
                }
            }

            if (compiledInversedExpression != null)
            {
                try
                {
                    if (targetType == typeof(bool) && value.GetType() == typeof(Visibility))
                        value = new BoolToVisibilityConverter(FalseToVisibility)
                            .ConvertBack(value, targetType, null, culture);

                    if (value.GetType() == typeof(string) && compiledExpression.Expression.Type != value.GetType())
                        value = ParseStringToObject((string)value, compiledExpression.Expression.Type);

                    var source = compiledInversedExpression.Invoke(value);
                    return source;
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Binding error: calc converter can't invoke back expression " + parameter + ": " + e.Message);
                }
            }
            return null;
        }

        private object ParseStringToObject(string value, Type type)
        {
            var res = System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            return res;
        } 

        #endregion
        
        #region IMultiValueConverter
        
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (compiledExpression == null)
            {
                if ((compiledExpression = compileExpression(values, (string)parameter)) == null)
                    return null;
            }

            try
            {
                var result = compiledExpression.Invoke(values);

                if (!StringFormatDefined)
                {
                    if (targetType == typeof(Visibility))
                    {
                        result = new BoolToVisibilityConverter(FalseToVisibility)
                                        .Convert(result, targetType, null, culture);
                    }

                    if (targetType == typeof(String))
                        result = String.Format(CultureInfo.InvariantCulture, "{0}", result);
                }
                return result;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Binding error: calc converter can't invoke expression " + compiledExpression.ExpressionText + ": " + e.Message);
                return null;
            }
        }

        private Lambda compileExpression(Object[] values, string expressionTemplate, bool convertBack = false, List<Type> targetTypes = null)
        {
            try
            {
                Lambda res = null;

                var needCompile = false;
                // we can't determine value type if value is null
                // so, binding Path = (a == null) ? "a" : "b" is permitted
                if (convertBack)
                    needCompile = true;
                else
                if (values.Contains(null))
                {
                    Trace.WriteLine("Binding error: one of source fields is null in binding init, return NULL");
                }
                else
                    if (values.Contains(DependencyProperty.UnsetValue))
                    {
                        Trace.WriteLine("Binding error: one of source fields is Unset, return null");
                    }
                    else
                    {
                        needCompile = true;
                    }

                if (needCompile)
                {
                    var argumentsTypes = convertBack ? targetTypes : values.Select(v => v.GetType()).ToList();
                    res = compileExpression(argumentsTypes, expressionTemplate);
                }

                return res;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Binding error: calc converter can't convert expression" + expressionTemplate + ": " + e.Message);
                return null;
            }
        }

        private Lambda compileExpression(List<Type> argumentsTypes, string expressionTemplate)
        {
            for (int i = 0; i < argumentsTypes.Count(); i++)
            {
                expressionTemplate = expressionTemplate.Replace("{" + i.ToString() + "}", getVariableName(i));
            }

            var parametersDefinition = new List<Parameter>();

            for (var i = 0; i < argumentsTypes.Count(); i++)
            {
                parametersDefinition.Add(
                    new Parameter(getVariableName(i), argumentsTypes[i])
                );
            }

            var compiledExpression = parser.Parse(expressionTemplate, parametersDefinition.ToArray());

            return compiledExpression;
        }

        /// <summary>
        /// Returns string of one char, following from 'a' on i positions (1 -> b, 2 -> c)
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private string getVariableName(int i)
        {
            return new string( new[] { (Char)(i + (int)'a') });
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        } 

        #endregion
    }
}
