using System;
using System.IO;
using System.Collections.Generic;

namespace lab1
{
    class DMPA
    {
        static readonly int M = 7; //Количество элементов алфавита автомата
        static readonly int N = 6; //Количество состояний автомата
        static readonly int G = 3; //Количество элементов алфавита магазина
        static int P = 0; //Количество пар входного символа и символа с вершины магазина
                                    //Список допустимых типов переменных
       

        enum ST : int { start = 0, getClass, getName, getParams, getParamName, moreParams, halt }; //Список состояний автомата.
                                                                                     //Статусы ошибок возвращаемые функциями
        enum pN : int { Spc_Empty=0, Letter_Empty, Spc_Name, Letter_Name, Number_Empty, Spc_Params,
                        Less_Empty, Less_Params, Great_Params, Halt_Empty,  Letter_Params,
                        Number_Params, Comma_Params, Spc_MoreParams, Letter_MoreParams};
        public enum RetCode : int { Success = 0, SyntaxError, TypeError, AlreadyDefinedError, ParamError }
        static List<char>[] alphabet; //Алфавит автомата
        static char[] stack_alphabet = new char[] { 'n', '+', 'm' }; //Алфавит магазина.  
                                                                              //n - читаем имя класса 
                                                                              //+ - параметры
                                                                              //a - надо добавить еще один параметр 
        static Stack<char> stack = new Stack<char>();
        const char NOTHING = '\0';
        const char POP = '\r';
        
        static readonly int EMPTY = -1;   //Магазин должен быть пуст
        static readonly int UNKNOWN = -2; //Используется как начальное неопределенное значение 
                                          //при определении принадлежнасти символа алфавиту магазина.
        static int[,] pairs; // массив пар: входной символ, вершина стека
        static int k = 0; //Общий счетчик входа
        static int cur_line = 1; //Счетчик строк файла
        static int cur_line_sym = 0; //Счетчик символа в текущей строке
                                     //Словарь для хранения переменных
        //Элемент таблицы переходов
        struct DeltaCell
        {
            public ST nextState;
            public char stack;
            public string action;
            DeltaCell(ST nextState=ST.start, char stack=NOTHING, string action="")
            {
                this.nextState = nextState;
                this.stack = stack;
                this.action = action;
            }
        }

        //Таблица переходов
        static DeltaCell[,] delta;

        //Буфер ввода
        static string buffer;
        //имя универсального типа
        static string classname = "";
        //Текущий тип переменных
        static string currentType;

        static StreamReader file;

        //Функция чтения фходного файла. Пока в файле есть символы считывает по одному символу и возвращает его.
        //Если достигнут конец файла то возвращает конец входной цепочки. 
        static char getNext()
        {
            char c = new char();
            if (!file.EndOfStream)
            {
                c = (char)file.Read();
                return c;
            }
            return (char)0;
        }

        //Основной алгоритм конечного автомата
        public static RetCode runDMPA()
        {
            ST curState = ST.start; //Текущее состояние автомата. Начальное: start
            char input;

            while (true)
            {
                input = getNext();

                k++;
                cur_line_sym++;
                if (input == '\n') { cur_line++; cur_line_sym = 0; }

                int al_idx = -1;

                //Поиск входного символа в алфавите
                for (int i = 0; i < alphabet.Length; i++)
                {
                    if (alphabet[i].Contains(input))
                    {
                        al_idx = i;
                        break;
                    }
                }

                if (al_idx == -1) return RetCode.SyntaxError;

                //Определение необходимого состояния стека
                int st_idx = UNKNOWN;

                if (stack.Count== 0)
                    st_idx = EMPTY;
                else
                {
                    char stack_top = stack.Peek();
                    for (int i = 0; i < G; i++)
                    {
                        if (stack_top == stack_alphabet[i])
                        {
                            st_idx = i;
                            break;
                        }
                    }
                }

                //Поиск пары вход/стек
                int pair = -1;
                for (int i = 0; i < P; i++)
                {
                    if ((pairs[i, 0] == al_idx) && (pairs[i, 1] == st_idx))
                    {
                        pair = i;
                        break;
                    }
                }

                //Пара не найдена
                if (pair == -1) return RetCode.SyntaxError;

                //Сейчас у нас есть текущее состояние и пара Символ/магазин

                //Проверяем есть ли дла этой пары переход в таблице
                DeltaCell curCell = delta[(int)curState, pair];
                //Если переменная магазина не проинициализированна значит переход 
                //в таблицу не занесен. Можно было также проверить переменную действия.
                if (curCell.action == null)
                    return RetCode.SyntaxError;

                //Разбор успешно окончен. Достигнуто финальное состояние.
                if (curCell.nextState == ST.halt)
                    return RetCode.Success;

                //Есть ли действия в текущем переходе?
                switch (curCell.action)
                {
                    case "addBuf":
                        addToBuffer(input);
                        break;
                    case "checkClass":
                        if (buffer != "class")
                            return RetCode.SyntaxError;
                        buffer = "";
                        break;
                    case "addName":
                        classname = buffer;
                        buffer = "";
                        break;
                    case "addParam":
                        RetCode result = addParam();
                        if (result != RetCode.Success)
                            return result;
                        break;
                }

                //Следующий переход
                if (curCell.stack != NOTHING)
                {
                    if (curCell.stack == POP)
                        stack.Pop();
                    else
                        stack.Push(curCell.stack);
                }

                curState = curCell.nextState;
            }
        }

        public static void Main(string[] args)
        {
            alphabet = new List<char>[M];
            //Заполняем элементы алфавита
            //Элементы a-z, A-Z и _
            alphabet[0] = new List<char> { };
            for (char ch = 'a'; ch <= 'z'; ch++) alphabet[0].Add(ch);
            for (char ch = 'A'; ch <= 'Z'; ch++) alphabet[0].Add(ch);
            alphabet[0].Add('_');

            //Элементы 0-9 
            alphabet[1] = new List<char> { };
            for (char ch = '0'; ch <= '9'; ch++) alphabet[1].Add(ch);

            //Элементы разделители 
            alphabet[2] = new List<char> { ' ', '\r', '\n', '\t' };

            //Элемент ','
            alphabet[3] = new List<char> { ',' };


            //Элемент '<'
            alphabet[4] = new List<char> { '<' };

            //Элемент '>'
            alphabet[5] = new List<char> { '>' };

            //Элемент конец входной цепочки
            alphabet[6] = new List<char> { (char)0 };
            //Spc_Empty = 0, Letter_Empty, Spc_Name, Letter_Name, Number_Empty, Spc_Params,
            //Less_Empty, Less_Params, Great_Empty, Spc_End, Halt_End, Letter_Params
            P = 15;
            pairs = new int[,] { /*0: Spc_Empty */{2, EMPTY }, /*1: Letter_Empty */{0, EMPTY },  /*2: Spc_Name */ { 2, 0}, 
                                 /*3 Letter_Name */ {0, 0}, /*4 Number_Empty */ {1,EMPTY }, /*5 Spc_Params*/ {2, 1 },
                                 /*6 Less_Empty */{4,EMPTY }, /*7 Less_Params */ {4, 1}, /*8 Great_Params */ { 5, 1} , 
                                 /*9 Halt_EMPTY*/ {6,EMPTY}, /*10 Letter_Params*/ {0,1 }, /*11 Number_Params */{1,1 }, 
                                 /*12 Comma_Params */ {3,1}, /*13 Spc_MoreParams */ {2, 2}, /*14 Letter_MoreParams */{0, 2}
                                };

            //Инициализируем таблицу переходов
            delta = new DeltaCell[N, P];
            //0 начальные пробелы 
            delta[(int)ST.start, (int)pN.Spc_Empty].nextState = ST.start;
            delta[(int)ST.start, (int)pN.Spc_Empty].action = "";
            //1 встретили букву, читаем слово class 
            delta[(int)ST.start, (int)pN.Letter_Empty].nextState = ST.getClass;
            delta[(int)ST.start, (int)pN.Letter_Empty].action = "addBuf";
            //1 читаем слово class 
            delta[(int)ST.getClass, (int)pN.Letter_Empty].nextState = ST.getClass;
            delta[(int)ST.getClass, (int)pN.Letter_Empty].action = "addBuf";
            //0 пробел - проверяем что считанное слово class, идем читать имя
            delta[(int)ST.getClass, (int)pN.Spc_Empty].nextState = ST.getName;
            delta[(int)ST.getClass, (int)pN.Spc_Empty].stack = 'n';
            delta[(int)ST.getClass, (int)pN.Spc_Empty].action = "checkClass";
            // пропускаем пробелы
            delta[(int)ST.getName, (int)pN.Spc_Name].nextState = ST.getName;
            delta[(int)ST.getName, (int)pN.Spc_Name].action = "";
            // Первая буква
            delta[(int)ST.getName, (int)pN.Letter_Name].nextState = ST.getName;
            delta[(int)ST.getName, (int)pN.Letter_Name].stack = POP;
            delta[(int)ST.getName, (int)pN.Letter_Name].action = "addBuf";
            //
            delta[(int)ST.getName, (int)pN.Letter_Empty].nextState = ST.getName;
            delta[(int)ST.getName, (int)pN.Letter_Empty].action = "addBuf";
            //4 цифра - читаем имя класса
            delta[(int)ST.getName, (int)pN.Number_Empty].nextState = ST.getName;
            delta[(int)ST.getName, (int)pN.Number_Empty].action = "addBuf";
            // < добавляем имя класса - идем за параметрами 
            delta[(int)ST.getName, (int)pN.Less_Empty].nextState = ST.getParams;
            delta[(int)ST.getName, (int)pN.Less_Empty].stack = '+';
            delta[(int)ST.getName, (int)pN.Less_Empty].action = "addName";
            // пробел добавляем имя класса - ждем параметры
            delta[(int)ST.getName, (int)pN.Spc_Empty].nextState = ST.getName;
            delta[(int)ST.getName, (int)pN.Spc_Empty].stack = '+';
            delta[(int)ST.getName, (int)pN.Spc_Empty].action = "addName";
            // пробел добавляем имя класса - ждем параметры
            delta[(int)ST.getName, (int)pN.Spc_Params].nextState = ST.getName;
            delta[(int)ST.getName, (int)pN.Spc_Params].action = "";
            // < идем в getParams 
            delta[(int)ST.getName, (int)pN.Less_Params].nextState = ST.getParams;
            delta[(int)ST.getName, (int)pN.Less_Params].action ="";
            //0 проверяем имя класса - ждем параметры
            delta[(int)ST.getName, (int)pN.Spc_Params].nextState = ST.getName;
            delta[(int)ST.getName, (int)pN.Spc_Params].action = "";
            // < идем в getParams
            delta[(int)ST.getName, (int)pN.Less_Params].nextState = ST.getParams;
            delta[(int)ST.getName, (int)pN.Less_Params].action = "";
            // пропускаем пробелы 
            delta[(int)ST.getParams, (int)pN.Spc_Params].nextState = ST.getParams;
            delta[(int)ST.getParams, (int)pN.Spc_Params].action = "";
            // > закончили текущий параметр 
            delta[(int)ST.getParams, (int)pN.Great_Params].nextState = ST.getParams;
            delta[(int)ST.getParams, (int)pN.Great_Params].stack = POP;
            delta[(int)ST.getParams, (int)pN.Great_Params].action = "";
            // < встретили новый параметр
            delta[(int)ST.getParams, (int)pN.Less_Params].nextState = ST.getParams;
            delta[(int)ST.getParams, (int)pN.Less_Params].stack = '+';
            delta[(int)ST.getParams, (int)pN.Less_Params].action = "";
            // встретили букву, читаем имя параметра
            delta[(int)ST.getParams, (int)pN.Letter_Params].nextState = ST.getParamName;
            delta[(int)ST.getParams, (int)pN.Letter_Params].action = "addBuf";
            // встретили букву, добавляем
            delta[(int)ST.getParamName, (int)pN.Letter_Params].nextState = ST.getParamName;
            delta[(int)ST.getParamName, (int)pN.Letter_Params].action = "addBuf";
            // встретили цифру, добавляем
            delta[(int)ST.getParamName, (int)pN.Number_Params].nextState = ST.getParamName;
            delta[(int)ST.getParamName, (int)pN.Number_Params].action = "addBuf";
            // встретили >, добавляем параметр, уходим в getParams
            delta[(int)ST.getParamName, (int)pN.Great_Params].nextState = ST.getParams;
            delta[(int)ST.getParamName, (int)pN.Great_Params].stack = POP;
            delta[(int)ST.getParamName, (int)pN.Great_Params].action = "addParam";
            // встретили <, добавляем параметр, уходим в getParams
            delta[(int)ST.getParamName, (int)pN.Less_Params].nextState = ST.getParams;
            delta[(int)ST.getParamName, (int)pN.Less_Params].stack = '+';
            delta[(int)ST.getParamName, (int)pN.Less_Params].action = "addParam";
            // встретили пробел, добавляем параметр, уходим в moreParams 
            delta[(int)ST.getParamName, (int)pN.Spc_Params].nextState = ST.moreParams;
            delta[(int)ST.getParamName, (int)pN.Spc_Params].action = "addParam";
            // встретили запятую, добавляем параметр, уходим в getParams 
            delta[(int)ST.getParamName, (int)pN.Comma_Params].nextState = ST.getParams;
            delta[(int)ST.getParamName, (int)pN.Comma_Params].stack = 'm';
            delta[(int)ST.getParamName, (int)pN.Comma_Params].action = "addParam";
            // встретили пробел, пропускаем
            delta[(int)ST.moreParams, (int)pN.Spc_Params].nextState = ST.moreParams;
            delta[(int)ST.moreParams, (int)pN.Spc_Params].action = "";
            // встретили >, идем в getParams
            delta[(int)ST.moreParams, (int)pN.Great_Params].nextState = ST.getParams;
            delta[(int)ST.moreParams, (int)pN.Great_Params].stack = POP;
            delta[(int)ST.moreParams, (int)pN.Great_Params].action = "";
            // встретили <, идем в getParams
            delta[(int)ST.moreParams, (int)pN.Less_Params].nextState = ST.getParams;
            delta[(int)ST.moreParams, (int)pN.Less_Params].stack = '+';
            delta[(int)ST.moreParams, (int)pN.Less_Params].action = "addParam";
            // встретили , идем в getParams
            delta[(int)ST.moreParams, (int)pN.Comma_Params].nextState = ST.getParams;
            delta[(int)ST.moreParams, (int)pN.Comma_Params].stack = 'm'; 
            delta[(int)ST.moreParams, (int)pN.Comma_Params].action = ""; 
            // нужно добавить еще один параметр, пробелы пропускаем
            delta[(int)ST.getParams, (int)pN.Spc_MoreParams].nextState = ST.getParams;
            delta[(int)ST.getParams, (int)pN.Spc_MoreParams].action = "";
            // нужно добавить еще один параметр. Буква. Дальше как обычно
            delta[(int)ST.getParams, (int)pN.Letter_MoreParams].nextState = ST.getParamName;
            delta[(int)ST.getParams, (int)pN.Letter_MoreParams].stack = POP;
            delta[(int)ST.getParams, (int)pN.Letter_MoreParams].action = "addBuf";
            // встретили запятую
            delta[(int)ST.getParams, (int)pN.Comma_Params].nextState = ST.getParams;
            delta[(int)ST.getParams, (int)pN.Comma_Params].action = "";

            // Заканчиваем разбор (считали посленюю >)
            delta[(int)ST.getParams, (int)pN.Spc_Empty].nextState = ST.getParams;
            delta[(int)ST.getParams, (int)pN.Spc_Empty].action = "";
            delta[(int)ST.getParams, (int)pN.Halt_Empty].nextState = ST.halt;
            delta[(int)ST.getParams, (int)pN.Halt_Empty].action = "";
            


            string input_file;

            RetCode result = 0;
            if (args.Length == 0)
                input_file = "input.txt";
            else
                input_file = args[0];
            try
            {
                file = File.OpenText(input_file);
                result = runDMPA();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.Write("\nНажмите любую клавишу...");
                Console.ReadKey();
                Environment.Exit(-1);
            }

            switch (result)
            {
                case RetCode.SyntaxError:
                    Console.WriteLine("Ошибка синтаксиса в строке: {0}, символ: {1}", cur_line, cur_line_sym);
                    break;
                case RetCode.TypeError:
                    Console.WriteLine("Недопустимый тип переменной: \"{0}\", строка: {1}", currentType, cur_line);
                    break;
                case RetCode.AlreadyDefinedError:
                    Console.WriteLine("Дублирование имени переменной \"{0}\", строка: {1}, символ: {2}",
                               buffer, cur_line, cur_line_sym - buffer.Length);
                    break;
                case RetCode.ParamError:
                    Console.WriteLine("Запрещено использовать имя класса в качестве имени параметра\"{0}\", строка: {1}, символ: {2}",
                               buffer, cur_line, cur_line_sym - buffer.Length);
                    break;
                case RetCode.Success:
                    Console.WriteLine("Описание корректное");
                    break;
            }
            
            Console.Write("\nНажмите любую клавишу...");
            Console.ReadKey();
        }

        //Действие автомата которое добавляет текущий символ последовательности в буфер
        static void addToBuffer(char input)
        {
            buffer += input;
        }

        //Действие автомата которое сохраняет значение типа текущего объявления 
        //в currentType, предварительно проверив на допустимость (имеется ли в 
        //списке types. По логике работы типы сохраняются с описанием массивов, 
        //если есть.
        static bool addType(char input)
        {

            currentType = buffer;
            buffer = Convert.ToString(input);

            string pureType; //"Чистый" тип данных (без скобок массива, и признака обнуляемого типа)
            int arrayStartsAt = currentType.IndexOf('[');

            //Убрать из типа данных описание массива для сравнения с types
            if (arrayStartsAt > -1)
            {
                pureType = currentType.Substring(0, arrayStartsAt);
            }
            else
                pureType = currentType;



            //Проверим, объявлен ли тип с символом @ то удалим его
            bool is_at = false;

            if (pureType.Contains("@"))
            {
                is_at = true;
                pureType = pureType.Replace("@", "");
            }

            //Проверим, является ли тип обнуляемым, и удалим что-бы проходила проверка на 
            //допустимый тип данных
            int nullSymPos = pureType.IndexOf('?');
            if (nullSymPos > -1)
            {

                pureType = pureType.Substring(0, nullSymPos).Trim();

                //Проверим на то что обнуляемый тип не является ссылочным
                if (pureType == "object" || pureType == "string" ||
                    pureType == "Object" || pureType == "String" ||
                    pureType == "System.Object" || pureType == "System.String")
                    return false;
            }

            //Если тип был объявлен с символом @ то проверим на допустимость объявления
            if (is_at)
                if (!pureType.Contains("."))
                    if (CSharpTypes.Contains(pureType))
                        return false;

            //Убрать из начала типа пространство имен System. В DotNetTypes типы хранятся без него.
            if (pureType.Contains("System."))
            {
                pureType = pureType.Substring(7);
                if (!DotNetTypes.Contains(pureType))
                    return false;
            }
            else if (!CSharpTypes.Contains(pureType) && !DotNetTypes.Contains(pureType))
            {
                return false;
            }

            return true;
        }

        //Действие добавляет параметр к типу 
        static RetCode addParam()
        {
            if (buffer == classname)
                return RetCode.ParamError;

            @params.Add(buffer);
            buffer = "";
            return RetCode.Success;
        }

        

        static List<string> @params = new List<string>();
        static readonly List<string> CSharpTypes = new List<string> {
            "bool", "byte", "sbyte", "char", "decimal",  "double",  "float",
            "int",  "uint",  "long",  "object", "short",  "ushort",  "string" };
        static readonly List<string> DotNetTypes = new List<string> {
            "Boolean", "Byte", "SByte", "Char", "Decimal", "Double", "Single",
            "Int32", "UInt32", "Int64", "Object", "Int16", "UInt16", "String" };
        //Список ключевых слов языка C#
        static readonly List<string> keywords = new List<string>   {
            "abstract", "add", "alias", "as", "ascending", "async", "await", "base",
            "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "descending", "do", "double", "dynamic",
            "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float",
            "for", "foreach", "from", "get", "global", "goto", "group", "if", "implicit", "in", "int",
            "interface", "internal", "into", "is", "join", "let", "lock", "long", "namespace",
            "new", "null", "object", "operator", "orderby", "out", "override", "params", "partial",
            "private", "protected", "public", "readonly", "ref", "remove", "return", "sbyte", "sealed",
            "select", "set", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "value", "var", "virtual", "void", "volatile", "where", "while"
    };
    }
}
