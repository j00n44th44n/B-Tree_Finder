using System;
using System.CodeDom;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EDA_PROJECT_1415;

namespace MyFinder
{
    #region Finder
    public class MyFinder : IFinder
    {
        #region Variables
        int t;
        StreamManager manager;
        private BTree path;
        private BTree date;
        private BTree size;
        private bool open;

        public static Func<IFile, IFile, int>[] comparer =
        {
            (x, y) => StringComparer(x.Address,y.Address),
            (x, y) => x.CreationDate.CompareTo(y.CreationDate),
            (x, y) => x.Size.CompareTo(y.Size)
        };
        #endregion

        #region Constructor
        public MyFinder(int t = 5)
        {
            this.t = t;
            open = false;
        }
        #endregion

        #region Metodos
        public void Open(Stream db)
        {
            if (open)
                throw new InvalidOperationException("Debe CERRAR el stream antes de poder abrirlo de nuevo");

            if(!db.CanRead)
            {
                var q = new FileStream("test.txt",FileMode.Open);
                manager = new StreamManager(t, q);
                open = true;
                if (q.Length == 0)
                {
                    path = new BTree(t, comparer[0], manager);
                    path.manager.OpenStream();
                    date = new BTree(t, comparer[1], manager);
                    date.manager.OpenStream();
                    size = new BTree(t, comparer[2], manager);
                    size.manager.OpenStream();
                }
                else
                {
                    var trees = manager.LoadBTrees();
                    path = trees.item1;
                    path.manager.OpenStream();
                    date = trees.item2;
                    date.manager.OpenStream();
                    size = trees.item3;
                    size.manager.OpenStream();
                }
            }
            else
            {
                manager = new StreamManager(t, db);
                open = true;
                if (db.Length == 0)
                {
                    path = new BTree(t, comparer[0], manager);
                    path.manager.OpenStream();
                    date = new BTree(t, comparer[1], manager);
                    date.manager.OpenStream();
                    size = new BTree(t, comparer[2], manager);
                    size.manager.OpenStream();
                }
                else
                {
                    var trees = manager.LoadBTrees();
                    path = trees.item1;
                    path.manager.OpenStream();
                    date = trees.item2;
                    date.manager.OpenStream();
                    size = trees.item3;
                    size.manager.OpenStream();
                }
            }
        }
        public void Close()
        {
            manager.Close();
            open = false;
            path = null;
            date = null;
            size = null;
        }

        public void AddFile(IFile file)
        {
            if (!open)
                throw new InvalidOperationException("Se debe abrir primero el Stream antes de intentar esta accion");

            var f = new File(file);
            path.Add(f);
            date.Add(f);
            size.Add(f);
        }

        public bool FindByAddress(string address)
        {
            foreach (var item in path.Searcher(x => StringComparer(x.Address, address)))
                return true;
            return false;
        }

        static string BelongTo(string address, bool backSlash)
        {
            for (int i = address.Length - 1; i >= 0; i--)
                if (address[i] == '\\')
                {
                    if (backSlash) i++;
                    return address.Substring(0, i);
                }
            return string.Empty;
        }

        static int StringComparer(string a1, string a2)
        {
            string f1 = BelongTo(a1, false),
                   f2 = BelongTo(a2, false);
            int result = f1.CompareTo(f2);
            return result != 0 ? result : a1.CompareTo(a2);
        }
        #endregion

        #region Iteradores

        public IEnumerable<IFile> FindFilesIn(string directoryAddress)
        {
            Func<IFile, int> condition = x =>
            {
                var folder = BelongTo(x.Address, directoryAddress[directoryAddress.Length - 1] == '\\');
                return folder.CompareTo(directoryAddress);
            };
            return path.Searcher(condition);
        }
        public IEnumerable<IFile> FindByDate(DateTime from, DateTime to)
        {
            Func<IFile, int> condition = x =>
            {
                int result = x.CreationDate.CompareTo(from);
                if (result < 0) return result;
                result = x.CreationDate.CompareTo(to);
                if (result > 0) return result;
                return 0;
            };
            return date.Searcher(condition);
        }
        public IEnumerable<IFile> FindBySize(long size)
        {
            return this.size.Searcher(x => x.Size.CompareTo(size));
        }
        public IEnumerable<IFile> FindLarger(long size)
        {
            return this.size.Searcher(x => x.Size.CompareTo(size) > 0 ? 0 : -1);
        }
        public IEnumerable<IFile> FindSmaller(long size)
        {
            return this.size.Searcher(x => x.Size.CompareTo(size) < 0 ? 0 : 1);
        }
        #endregion
    }
    #endregion

    #region BTree

    class BTree
    {
        #region Variables
        int t;
        Node root;
        public StreamManager manager;
        Func<IFile, IFile, int> comparer;
        #endregion

        #region Constructor
        public BTree(int t, Func<IFile, IFile, int> comparer, StreamManager manager)
        {
            this.t = t;
            manager.OpenStream();
            this.manager = manager;
            this.comparer = comparer;
            root = AllocateNode();
            root.leaf = true;
            manager.UpdateRoot(root.offset, Array.IndexOf(MyFinder.comparer, comparer));
            manager.DiskWrite(root);
        }
        public BTree(int t, long root, Func<IFile, IFile, int> comparer, StreamManager manager)
        {
            this.t = t;
            this.manager = manager;
            this.comparer = comparer;
            this.root = manager.DiskRead(root);
        }
        #endregion

        #region Metodos
        public void Add(IFile k)
        {
            Node r = this.root;
            if (r.n == 2 * t - 1)
            {
                //La raíz está llena. Separarla y crear una nueva
                Node s = AllocateNode();
                this.root = s;
                s.leaf = false;
                s.c[0] = r.offset;

                SplitChild(r, s, 0);
                InsertNonFull(s, k);
                manager.UpdateRoot(s.offset, Array.IndexOf(MyFinder.comparer, comparer));
            }
            else InsertNonFull(r, k);
        }
        private void InsertNonFull(Node x, IFile k)
        {
            int index = x.n - 1;
            if (x.leaf)
            {
                while (index >= 0 && comparer(k, x.key[index]) < 0)
                {
                    x.key[index + 1] = x.key[index];
                    index--;
                }
                x.key[index + 1] = k;
                x.n++;
                manager.DiskWrite(x);
            }
            else
            {
                while (index >= 0 && comparer(k, x.key[index]) < 0)
                    index--;
                index++;
                Node child = manager.DiskRead(x.c[index]);
                if (child.n == 2 * t - 1)
                {
                    SplitChild(child, x, index);
                    if (comparer(k, x.key[index]) >= 0)
                        child = manager.DiskRead(x.c[index + 1]);
                }
                InsertNonFull(child, k);
            }
        }
        private void SplitChild(Node y, Node x, int i)
        {
            Node z = AllocateNode();
            z.leaf = y.leaf;
            z.n = t - 1;
            for (int j = 0; j < t - 1; j++)
                z.key[j] = y.key[j + t];
            y.n = t - 1;
            if (!y.leaf)
                for (int j = 0; j < t; j++)
                    z.c[j] = y.c[j + t];
            for (int j = x.n; j > i; j--)
                x.c[j + 1] = x.c[j];
            x.c[i + 1] = z.offset;
            for (int j = x.n - 1; j >= i; j--)
                x.key[j + 1] = x.key[j];
            x.key[i] = y.key[t - 1];
            x.n++;
            manager.DiskWrite(y);
            manager.DiskWrite(z);
            manager.DiskWrite(x);
        }

        private Node AllocateNode()
        {
            var node = new Node(t);
            node.offset = manager.GetNewOffset();
            return node;
        }
        #endregion

        #region SuperFinder
        public IEnumerable<IFile> Searcher(Func<IFile, int> condition)
        {
            return Searcher(root, condition);
        }
        private IEnumerable<IFile> Searcher(Node node, Func<IFile, int> condition)
        {
            for (int i = 0; i < node.n; i++)
            {
                int c = condition(node.key[i]);
                if (c < 0)
                {
                    if (i == node.n - 1 && !node.leaf)
                    {
                        Node xci = manager.DiskRead(node.c[i + 1]);
                        foreach (var item in Searcher(xci, condition))
                            yield return item;
                    }
                }
                else if (c == 0)
                {
                    if (!node.leaf)
                    {
                        Node xci = manager.DiskRead(node.c[i]);
                        foreach (var item in Searcher(xci, condition))
                            yield return item;
                    }
                    yield return node.key[i];
                    if (i == node.n - 1 && !node.leaf)
                    {
                        Node xci = manager.DiskRead(node.c[i + 1]);
                        foreach (var item in Searcher(xci, condition))
                            yield return item;
                    }
                }
                else if (c > 0)
                {
                    if (!node.leaf)
                    {
                        Node xci = manager.DiskRead(node.c[i]);
                        foreach (var item in Searcher(xci, condition))
                            yield return item;
                    }
                    break;
                }
            }
        }
        #endregion
    }

    #endregion

    #region Nodo-BTree
    class Node
    {
        #region Variables
        public int n;
        public IFile[] key;
        public long[] c;
        public long offset;
        public bool leaf { get; set; }
        #endregion

        #region Constructor
        public Node(int t)
        {
            key = new IFile[t * 2 - 1];
            c = new long[t * 2];
        }
        #endregion
    }

    #endregion

    #region StreamManager

    internal class StreamManager
    {
        #region Variables
        Stream data;
        BinaryWriter bw;
        BinaryReader br;
        private int t, nodeSize, count;
        #endregion

        #region Constructor
        public StreamManager(int t, Stream db)
        {
            this.t = t;
            nodeSize = 5 + (2 * t - 1) * (274) + (2 * t) * 8;
            data = db;
            bw = new BinaryWriter(db, Encoding.ASCII);
            br = new BinaryReader(db, Encoding.ASCII);
        }
        #endregion

        #region Metodos
        public void DiskWrite(Node x)
        {
            data = new FileStream("test.txt",FileMode.OpenOrCreate,FileAccess.ReadWrite);
            data.Position = x.offset;
            bw.Write(x.n);
            bw.Write(x.leaf);
            for (int i = 0; i < x.n; i++)
            {
                IFile key = x.key[i];
                bw.Write(key.Address);
                bw.Write(key.CreationDate.ToBinary());
                bw.Write(key.Size);
            }
            if (!x.leaf)
                for (int i = 0; i < x.n + 1; i++)
                    bw.Write(x.c[i]);
        }
        public Node DiskRead(long offset)
        {
            data = new FileStream("test.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            Node node;
            data.Position = offset;
            node = new Node(t) { offset = offset };
            node.n = br.ReadInt32();
            node.leaf = br.ReadBoolean();
            node.key = new IFile[2 * t - 1];
            for (int i = 0; i < node.n; i++)
            {
                string address = br.ReadString();
                long binDate = br.ReadInt64();
                DateTime date = DateTime.FromBinary(binDate);
                long size = br.ReadInt64();
                node.key[i] = new File(address, date, size);
            }
            node.c = new long[2 * t];
            if (!node.leaf)
                for (int i = 0; i < node.n + 1; i++)
                    node.c[i] = br.ReadInt64();
            return node;
        }
        public Pair<BTree> LoadBTrees()
        {
            Pair<BTree> p = new Pair<BTree>();
            using (br)
            {
                data.Position = 0;
                var rootOffset = br.ReadInt64();
                p.item1 = new BTree(t, rootOffset, MyFinder.comparer[0], this);
                data.Position = 8;
                rootOffset = br.ReadInt64();
                p.item2 = new BTree(t, rootOffset, MyFinder.comparer[1], this);
                data.Position = 16;
                rootOffset = br.ReadInt64();
                p.item3 = new BTree(t, rootOffset, MyFinder.comparer[2], this);
                data.Position = 24;
                count = br.ReadInt32();
            }
            return p;

            #region old code
            //var roots = new byte[28];
            //data.Read(roots, 0, 28);

            //var trees = new BTree[3];
            //for (int i = 0; i < 3; i++)
            //{
            //    long rootPosition = BitConverter.ToInt64(roots, i * 8);
            //    trees[i] = new BTree(t, rootPosition, MyFinder.comparers[i], this);
            //}
            //count = BitConverter.ToInt32(roots, 24);
            //return trees;
            #endregion
        }
        public void Close()
        {
            data.Position = 24;
            bw.Write(count);
            data.Close();
        }
        public long GetNewOffset()
        {
            long pos = 28 + (long)count * nodeSize;
            count++;
            return pos;
        }
        public void UpdateRoot(long rootPosition, int treeIndex)
        {
            data.Position = treeIndex * 8;
            bw.Write(rootPosition);
        }
        public void OpenStream(string address = "test.txt")
        {
            if(!data.CanRead)
                data = new FileStream(address,FileMode.OpenOrCreate,FileAccess.ReadWrite);
        }

        #endregion
    }

    #endregion

    #region File
    public class File : IFile
    {
        #region Propiedades
        public string Address { get; set; }
        public DateTime CreationDate { get; set; }
        public long Size { get; set; }
        #endregion

        #region Constructor
        public File(IFile file)
        {
            Address = file.Address;
            CreationDate = file.CreationDate;
            Size = file.Size;
        }
        public File(string address, DateTime creationDate, long size)
        {
            Address = address;
            CreationDate = creationDate;
            Size = size;
        }
        #endregion
    }
    #endregion

    #region Pair

    public class Pair<T>
    {
        #region Variables
        public T item1;
        public T item2;
        public T item3;
        #endregion

        #region Constructor
        public Pair(T t1, T t2, T t3)
        {
            item1 = t1;
            item2 = t2;
            item3 = t3;
        }
        public Pair()
        {
        }
        #endregion
    }

    #endregion
}