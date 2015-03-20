using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class TrieNode<V> where V: class
    {
        public V Value { get; set; }
        public Char Key { get; private set; }
        public Dictionary<Char, TrieNode<V>> kids;
        public TrieNode<V> Mom { get; private set; }

        // Creates a new node with a letter and a parent.
        public TrieNode(Char letter, TrieNode<V> parent)
        {
            this.Key = letter;
            this.Mom = parent;
            this.kids = new Dictionary<Char, TrieNode<V>>();
            this.Value = null;
        }

        // Returns the child node of this node with the given letter
        // Else returns null
        public TrieNode<V> getKid(Char letter)
        {
            if (hasKid(letter))
            {
                return this.kids[letter];
            }
            return null;
        }

        // Returns number of leaves this node has.
        public int numOfKids()
        {
            return this.kids.Count;
        }

        // Returns true if this node is a leaf node.
        public Boolean isLastKid()
        {
            return this.kids.Count == 0;
            //return numOfKids();
        }

        // Having a value means that this letter node has child nodes.
        public Boolean hasValue()
        {
            return this.Value != null;
        }

        // Returns true if this node has a child node with the given letter.
        public Boolean hasKid(Char letter) {
            return this.kids.ContainsKey(letter);
        }

        // Returns the child node with the given letter of this node.
        // Adds a new kid if it isn't already this node's child node.
        public TrieNode<V> addKid(Char letter)
        {
            if (hasKid(letter))
            {
                return this.kids[letter];
            }
            else
            {
                TrieNode<V> newKid = new TrieNode<V>(letter, this);
                this.kids.Add(letter, newKid);
                return newKid;
            }
        }

        // Removes this child node with the given letter.
        public void removeKid(Char letter)
        {
            this.kids.Remove(letter);
        }

        // Return all terminal nodes in a list.
        public List<TrieNode<V>> getAllTerminalNodes()
        {
            List<TrieNode<V>> nodes = new List<TrieNode<V>>();
            if (isLastKid())
            {
                if (hasValue())
                {
                    nodes.Add(this);
                }
                return nodes;
            }
            else
            {
                // recursive case get all child nodes that lead to a value of this node
                foreach (TrieNode<V> n in this.kids.Values)
                {
                    nodes.AddRange(n.getAllTerminalNodes());
                }
                // base add the value of this node
                if (hasValue())
                {
                    nodes.Add(this);
                }
                return nodes;
            }
        }
        /*
        // Return all the descendants/values of this node in a list.
        public List<V> getAllValues() {
            if (isLastKid())
            {
                if (hasValue())
                {
                    return new List<V>(new V[] { this.Value });
                }
                else
                {
                    return new List<V>();
                }
            }
            else
            {
                List<V> kids = new List<V>();
                // recursive case get all child nodes that lead to a value of this node
                foreach (TrieNode<V> n in this.kids.Values)
                {
                    kids.AddRange(n.getAllValues());
                }
                // base add the value of this node
                if (hasValue())
                {
                    kids.Add(this.Value);
                }
                return kids;
            }
        }

        // Get all keys or letters that also are terminals or have a value.
        public List<Char> keySet()
        {
            if (isLastKid())
            {
                if (hasValue())
                {
                    return new List<Char>(this.Key);
                }
                else
                {
                    return new List<Char>();
                }
            }
            else
            {
                List<Char> keys = new List<Char>();
                foreach (TrieNode<V> n in this.kids.Values)
                {
                    keys.AddRange(n.keySet());
                }
                if (hasValue())
                {
                    keys.Add(this.Key);
                }
                return keys;
            }
        }*/
    }
}