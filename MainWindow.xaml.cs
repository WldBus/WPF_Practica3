using System;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;

namespace EngineeringCalculator
{
    public partial class MainWindow : Window
    {
        private string currentInput = "";
        private string expressionHistory = "";
        private bool newInput = true;
        private bool calculated = false;
        private string lastOperation = "";
        private string lastNumber = "";
        private string lastOperator = "";
        private string lastOperand = "";
        private bool shouldRepeatOperation = false;

        public MainWindow()
        {
            InitializeComponent();
            ResultText.Text = "0";
        }

        private void UpdateDisplay()
        {
            ExpressionText.Text = expressionHistory;
            ResultText.Text = string.IsNullOrEmpty(currentInput) ? "0" : currentInput;
        }

        private void Input_Click(object sender, RoutedEventArgs e)
        {
            string digit = ((Button)sender).Content.ToString();

            if (calculated || shouldRepeatOperation)
            {
                currentInput = "";
                expressionHistory = "";
                calculated = false;
                shouldRepeatOperation = false;
            }

            if (newInput)
            {
                currentInput = digit;
                newInput = false;
            }
            else
            {
                if (currentInput == "0")
                    currentInput = digit;
                else
                    currentInput += digit;
            }

            UpdateDisplay();
        }

        private void ConstButton_Click(object sender, RoutedEventArgs e)
        {
            string val = ((Button)sender).Content.ToString();
            if (val == "π")
                currentInput = Math.PI.ToString(CultureInfo.InvariantCulture);
            else if (val == "e")
                currentInput = Math.E.ToString(CultureInfo.InvariantCulture);

            newInput = false;
            UpdateDisplay();
        }

        private void ClearEntry_Click(object sender, RoutedEventArgs e)
        {
            currentInput = "";
            expressionHistory = "";
            ResultText.Text = "0";
            ExpressionText.Text = "";
            newInput = true;
            calculated = false;
            lastOperation = "";
            lastNumber = "";
            lastOperator = "";
            lastOperand = "";
            shouldRepeatOperation = false;
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (currentInput.Length > 0)
            {
                currentInput = currentInput.Substring(0, currentInput.Length - 1);
                if (currentInput.Length == 0)
                {
                    currentInput = "0";
                    newInput = true;
                }
            }
            UpdateDisplay();
        }

        private void Operator_Click(object sender, RoutedEventArgs e)
        {
            string op = ((Button)sender).Content.ToString();
            string mathOp = "";

            switch (op)
            {
                case "×": mathOp = " * "; lastOperator = "*"; break;
                case "÷": mathOp = " / "; lastOperator = "/"; break;
                case "−": mathOp = " - "; lastOperator = "-"; break;
                case "+": mathOp = " + "; lastOperator = "+"; break;
            }

            if (!string.IsNullOrEmpty(currentInput))
            {
                expressionHistory += currentInput + mathOp;
                lastOperand = currentInput;
                currentInput = "";
                shouldRepeatOperation = false;
            }
            else if (!string.IsNullOrEmpty(expressionHistory))
            {
                expressionHistory = expressionHistory.TrimEnd();
                int lastSpace = expressionHistory.LastIndexOf(' ');
                if (lastSpace > 0)
                {
                    expressionHistory = expressionHistory.Substring(0, lastSpace) + mathOp;
                    lastOperator = mathOp.Trim();
                }
            }

            newInput = true;
            calculated = false;
            UpdateDisplay();
        }

        private void FunctionButton_Click(object sender, RoutedEventArgs e)
        {
            string func = ((Button)sender).Content.ToString();

            if (!string.IsNullOrEmpty(currentInput))
            {
                if (func == "sin" || func == "cos" || func == "tg")
                {
                    // Преобразуем в радианы
                    expressionHistory += $"Math.{ToMathFunc(func)}({currentInput} * {Math.PI} / 180)";
                }
                else
                {
                    expressionHistory += $"{func}({currentInput})";
                }
                currentInput = "";
            }
            else
            {
                expressionHistory += func + "(";
            }

            newInput = true;
            shouldRepeatOperation = false;
            UpdateDisplay();
        }

        private string ToMathFunc(string func)
        {
            return func switch
            {
                "sin" => "Sin",
                "cos" => "Cos",
                "tg" => "Tan",
                _ => func
            };
        }

        private void Equals_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string eval;
                if (shouldRepeatOperation && !string.IsNullOrEmpty(lastOperator) && !string.IsNullOrEmpty(lastOperand))
                {
                    eval = ResultText.Text + lastOperator + lastOperand;
                    expressionHistory = ResultText.Text + " " + lastOperator + " " + lastOperand + " = ";
                }
                else if (!string.IsNullOrEmpty(currentInput) && !string.IsNullOrEmpty(expressionHistory))
                {
                    eval = expressionHistory + currentInput;
                    expressionHistory = eval + " = ";
                    lastOperand = currentInput;
                    shouldRepeatOperation = true;
                }
                else
                {
                    return;
                }

                eval = eval.Replace("×", "*")
                          .Replace("÷", "/")
                          .Replace("−", "-")
                          .Replace(",", ".")
                          .Replace("log", "Math.Log10")
                          .Replace("ln", "Math.Log");

                eval = ReplacePowerOperators(eval);

                double result = EvaluateExpression(eval);
                currentInput = result.ToString(CultureInfo.InvariantCulture);
                newInput = true;
                calculated = true;
                UpdateDisplay();
            }
            catch
            {
                ResultText.Text = "Ошибка";
            }
        }

        private double EvaluateWithCSharpScript(string expr)
        {
            var scriptOptions = Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                .AddReferences(typeof(Math).Assembly)
                .AddImports("System");

            var result = Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript
                .EvaluateAsync<double>(expr, scriptOptions).Result;

            return result;
        }

        private double EvaluateExpression(string expr)
        {
            try
            {
                expr = expr.Replace(",", ".");

                // Заменяем функции на Math.*
                expr = expr.Replace("sin", "Math.Sin")
                           .Replace("cos", "Math.Cos")
                           .Replace("tg", "Math.Tan")
                           .Replace("log", "Math.Log10")
                           .Replace("ln", "Math.Log")
                           .Replace("π", Math.PI.ToString(CultureInfo.InvariantCulture))
                           .Replace("e", Math.E.ToString(CultureInfo.InvariantCulture))
                           .Replace("√", "Math.Sqrt");

                expr = ReplacePowerOperators(expr);

                // Простой способ: если нет Math. — используем DataTable
                if (!expr.Contains("Math."))
                {
                    var dt = new DataTable();
                    dt.Locale = CultureInfo.InvariantCulture;
                    var result = dt.Compute(expr, "");
                    return Convert.ToDouble(result);
                }

                // Пытаемся скомпилировать через CSharpScript
                return EvaluateWithCSharpScript(expr);
            }
            catch
            {
                throw new Exception("Ошибка вычисления выражения");
            }
        }


        private void Square_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentInput)) return;

            try
            {
                double num = double.Parse(currentInput, CultureInfo.InvariantCulture);
                currentInput = (num * num).ToString(CultureInfo.InvariantCulture);
                expressionHistory = $"sqr({num}) = ";
                newInput = true;
                calculated = true;
                shouldRepeatOperation = false;
                UpdateDisplay();
            }
            catch
            {
                ResultText.Text = "Ошибка";
            }
        }

        private void Negate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentInput)) return;

            if (currentInput.StartsWith("-"))
                currentInput = currentInput.Substring(1);
            else
                currentInput = "-" + currentInput;

            UpdateDisplay();
        }

        private void Sqrt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentInput)) return;

            try
            {
                double num = double.Parse(currentInput, CultureInfo.InvariantCulture);
                currentInput = Math.Sqrt(num).ToString(CultureInfo.InvariantCulture);
                expressionHistory = $"√({num}) = ";
                newInput = true;
                calculated = true;
                shouldRepeatOperation = false;
                UpdateDisplay();
            }
            catch
            {
                ResultText.Text = "Ошибка";
            }
        }

        private void Reciprocal_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentInput)) return;

            try
            {
                double num = double.Parse(currentInput, CultureInfo.InvariantCulture);
                currentInput = (1 / num).ToString(CultureInfo.InvariantCulture);
                expressionHistory = $"1/({num}) = ";
                newInput = true;
                calculated = true;
                shouldRepeatOperation = false;
                UpdateDisplay();
            }
            catch
            {
                ResultText.Text = "Ошибка";
            }
        }

        private void Abs_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentInput)) return;

            try
            {
                double num = double.Parse(currentInput, CultureInfo.InvariantCulture);
                currentInput = Math.Abs(num).ToString(CultureInfo.InvariantCulture);
                expressionHistory = $"abs({num}) = ";
                newInput = true;
                calculated = true;
                shouldRepeatOperation = false;
                UpdateDisplay();
            }
            catch
            {
                ResultText.Text = "Ошибка";
            }
        }

        private void Factorial_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentInput)) return;

            try
            {
                int n = int.Parse(currentInput);
                int result = 1;
                for (int i = 2; i <= n; i++)
                    result *= i;
                currentInput = result.ToString();
                expressionHistory = $"fact({n}) = ";
                newInput = true;
                calculated = true;
                shouldRepeatOperation = false;
                UpdateDisplay();
            }
            catch
            {
                ResultText.Text = "Ошибка";
            }
        }

        private void Power_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentInput))
            {
                currentInput = "0";
            }
            expressionHistory += currentInput + " ^ ";
            currentInput = "";
            newInput = true;
            shouldRepeatOperation = false;
            UpdateDisplay();
        }

        private void TenPower_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentInput))
            {
                expressionHistory += $"Math.Pow(10, {currentInput})";
                currentInput = "";
            }
            else
            {
                expressionHistory += "Math.Pow(10,";
            }
            newInput = true;
            shouldRepeatOperation = false;
            UpdateDisplay();
        }

        private void Log_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentInput))
            {
                expressionHistory += $"log({currentInput})";
                currentInput = "";
            }
            else
            {
                expressionHistory += "log(";
            }

            newInput = true;
            shouldRepeatOperation = false;
            UpdateDisplay();
        }

        private void Ln_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentInput))
            {
                expressionHistory += $"ln({currentInput})";
                currentInput = "";
            }
            else
            {
                expressionHistory += "ln(";
            }

            newInput = true;
            shouldRepeatOperation = false;
            UpdateDisplay();
        }

        private void Decimal_Click(object sender, RoutedEventArgs e)
        {
            if (calculated)
            {
                currentInput = "0.";
                calculated = false;
                newInput = false;
                shouldRepeatOperation = false;
                UpdateDisplay();
                return;
            }

            if (newInput)
            {
                currentInput = "0.";
                newInput = false;
                shouldRepeatOperation = false;
            }
            else if (!currentInput.Contains("."))
            {
                currentInput += ".";
            }

            UpdateDisplay();
        }

        private string ReplacePowerOperators(string expr)
        {
            return Regex.Replace(expr, @"(\d+(\.\d+)?|\w+)\s*\^\s*(\d+(\.\d+)?|\w+)", "Math.Pow($1,$3)");
        }
    }
}
