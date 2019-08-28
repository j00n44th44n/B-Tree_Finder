using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tester
{
    class Program
    {
        static Random r = new Random();
        static Stopwatch crono = new Stopwatch();

        static void Main()
        {
            Stream s = new FileStream("test.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var path = @"C:\jonathan\Media";
            var info = new DirectoryInfo(path);
            var crono = new Stopwatch();
            var files = new List<MyFinder.File>();
            MyFinder.MyFinder f = new MyFinder.MyFinder(5);
            f.Open(s);

            Console.WriteLine("Busqueda");
            crono.Start();
            foreach (var item in info.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var file = new MyFinder.File(item.FullName, item.CreationTime, item.Length);
                files.Add(file);
            }
            crono.Stop();
            Console.WriteLine("{0} en {1}", files.Count, (float)crono.ElapsedMilliseconds / 1000);
            crono.Reset();
            Console.WriteLine("Insercion");
            crono.Start();
            for (int i = 0; i < files.Count; i++)
            {
                f.AddFile(files[i]);
            }
            crono.Stop();
            Console.WriteLine("{0} en {1}", files.Count, (float)crono.ElapsedMilliseconds / 1000);
            int count = 0;
            //foreach (var q in f.FindFilesIn(@"C:\jonathan\Media\music"))
            //{
            //    count++;
            //    Console.WriteLine(q.Address);
            //}
            //Console.WriteLine("{0} encontrados", count);
        }
    }
}
