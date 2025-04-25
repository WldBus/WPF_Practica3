using System;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

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
                expressionHistory += $"{func}({currentInput})";
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
                string displayExpr;

                if (shouldRepeatOperation && !string.IsNullOrEmpty(lastOperator) && !string.IsNullOrEmpty(lastOperand))
                {
                    eval = ResultText.Text + lastOperator + lastOperand;
                    displayExpr = ResultText.Text + " " + lastOperator + " " + lastOperand;
                }
                else if (!string.IsNullOrEmpty(currentInput) && !string.IsNullOrEmpty(expressionHistory))
                {
                    eval = expressionHistory + currentInput;
                    displayExpr = expressionHistory + currentInput;
                    lastOperand = currentInput;
                    shouldRepeatOperation = true;
                }
                else
                {
                    return;
                }

                // Преобразуем для вычисления, но сохраняем оригинальный вид для отображения
                string computeExpr = ConvertToComputeExpression(eval);
                displayExpr = CleanDisplayExpression(displayExpr);

                double result = EvaluateExpression(computeExpr);
                currentInput = result.ToString(CultureInfo.InvariantCulture);
                expressionHistory = displayExpr + " =";
                newInput = true;
                calculated = true;
                UpdateDisplay();
            }
            catch
            {
                ResultText.Text = "Ошибка";
            }
        }

        private string CleanDisplayExpression(string expr)
        {
            // Убираем лишние скобки и преобразования для отображения
            string displayExpr = expr.Replace("×", "*")
                                   .Replace("÷", "/")
                                   .Replace("−", "-");

            // Можно добавить дополнительные преобразования для красоты
            return displayExpr;
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
                var options = ScriptOptions.Default
                    .AddReferences(typeof(Math).Assembly)
                    .AddImports("System");

                return CSharpScript.EvaluateAsync<double>(expr, options).Result;
            }
            catch
            {
                // Если не удалось вычислить через Roslyn, пробуем через DataTable
                try
                {
                    var dt = new DataTable();
                    dt.Locale = CultureInfo.InvariantCulture;
                    return Convert.ToDouble(dt.Compute(expr, null));
                }
                catch
                {
                    throw new Exception("Ошибка вычисления выражения");
                }
            }
        }

        private string ConvertToComputeExpression(string expr)
        {
            string computeExpr = expr.Replace("×", "*")
                                     .Replace("÷", "/")
                                     .Replace("−", "-")
                                     .Replace(",", ".")
                                     .Replace("π", Math.PI.ToString(CultureInfo.InvariantCulture))
                                     .Replace("e", Math.E.ToString(CultureInfo.InvariantCulture));

            // ✅ Преобразуем логарифмы
            computeExpr = Regex.Replace(computeExpr, @"log\(([^()]+)\)", "Math.Log10($1)", RegexOptions.IgnoreCase);
            computeExpr = Regex.Replace(computeExpr, @"ln\(([^()]+)\)", "Math.Log($1)", RegexOptions.IgnoreCase);

            // ✅ Преобразуем тригонометрические функции в градусах
            computeExpr = Regex.Replace(computeExpr, @"sin\(([^()]+)\)",
                m => $"Math.Sin(({m.Groups[1].Value}) * Math.PI / 180)", RegexOptions.IgnoreCase);

            computeExpr = Regex.Replace(computeExpr, @"cos\(([^()]+)\)",
                m => $"Math.Cos(({m.Groups[1].Value}) * Math.PI / 180)", RegexOptions.IgnoreCase);

            computeExpr = Regex.Replace(computeExpr, @"tg\(([^()]+)\)",
                m => $"Math.Tan(({m.Groups[1].Value}) * Math.PI / 180)", RegexOptions.IgnoreCase);

            // ✅ Обработка степеней: 10^x → Math.Pow(10, x), a^b → Math.Pow(a, b)
            computeExpr = Regex.Replace(computeExpr, @"(\d+(\.\d+)?|\([^()]+\))\s*\^\s*(\d+(\.\d+)?|\([^()]+\))",
                "Math.Pow($1,$3)");

            return computeExpr;
        }


        private void Square_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentInput)) return;

            try
            {
                string number = currentInput;
                double num = double.Parse(number, CultureInfo.InvariantCulture);
                double result = num * num;

                // Для отображения: 6^2
                expressionHistory = $"{number}^2";

                // Для вычисления — сохраним квадрат как текущее значение
                currentInput = result.ToString(CultureInfo.InvariantCulture);

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
                expressionHistory += $"10^{currentInput}";
                double exponent = double.Parse(currentInput, CultureInfo.InvariantCulture);
                currentInput = Math.Pow(10, exponent).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                expressionHistory += "10^";
            }

            newInput = true;
            calculated = true;
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
