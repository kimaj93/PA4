using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class Trie<V> where V: class
    {
        public TrieNode<V> overallRoot;
        public Suggestor<V> sug { get; private set; }

        public Trie()
        {
            this.overallRoot = new TrieNode<V>('\0', null);
            this.sug = new Suggestor<V>(this.overallRoot);
        }

        // Put a new word into the trie with a number value which means 
        // that this char combination is a word in the dictionary. 
        public void put(String word, V value)
        {
            TrieNode<V> root = overallRoot;
            foreach (Char c in word)
            {
                // Add a new node if it doesn't exist with the given char
                // then return the node.
                // Percolating down the trie.
                root = root.addKid(c);
            }
            // the last character node will be assigned the given value
            // this is the last char in the word.
            root.Value = value;
        }

        // Remove the given word from the trie.
        public void remove(String word)
        {
            TrieNode<V> root = overallRoot;
            foreach (Char c in word)
            {
                // Percolate down the descendants with the each given character.
                root = root.getKid(c);
            }
            // At the last letter node, we set the value to null
            // because we are removing this word from the dictionary.
            root.Value = null;

            // Current node must be a leaf and its value must be null.
            // Percolate back up the trie
            // Will not enter loop if another node's value is mapped to the current node.
            while (root != overallRoot && !root.hasValue() && root.isLastKid())
            {
                // Percolate up the parents until the top of the trie is reached.
                // Remove each letter node backwards
                Char lastLetter = root.Key;
                root = root.Mom;
                root.removeKid(lastLetter);
            }

            // Clear the suggestor
            this.sug.clearPrefix();
        }

       /* // Returns true if there is more than one node in the trie.
        public Boolean hasKid()
        {
            return !this.overallRoot.isLastKid();
        }
        */
        /*public List<V> getAllKids()
        {
            return this.overallRoot.getAllValues();
        }
        */
        // Returns a list of chars that are terminals or the last letter of each word in the trie.
        public List<TrieNode<V>> terminalNodes()
        {
            return this.overallRoot.getAllTerminalNodes();
        }
    }
}