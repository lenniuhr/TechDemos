using System.Collections;
using System.Collections.Generic;

public class UniqueQueue<T> : IEnumerable<T>
{
    private HashSet<T> hashSet;
    private Queue<T> queue;


    public UniqueQueue()
    {
        hashSet = new HashSet<T>();
        queue = new Queue<T>();
    }


    public int Count
    {
        get
        {
            return hashSet.Count;
        }
    }

    public void Clear()
    {
        hashSet.Clear();
        queue.Clear();
    }


    public bool Contains(T item)
    {
        return hashSet.Contains(item);
    }


    public void Enqueue(T item)
    {
        if (hashSet.Add(item))
        {
            queue.Enqueue(item);
        }
    }

    public T Dequeue()
    {
        T item = queue.Dequeue();
        hashSet.Remove(item);
        return item;
    }

    public bool TryDequeue(out T result)
    {
        bool success = queue.TryDequeue(out result);
        if (success) hashSet.Remove(result);
        return success;
    }


    public T Peek()
    {
        return queue.Peek();
    }


    public IEnumerator<T> GetEnumerator()
    {
        return queue.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return queue.GetEnumerator();
    }
}
