using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class Suggestor<V> where V: class
    {
        private TrieNode<V> overallRoot;
        private TrieNode<V> root;
        public String currentPrefix { get; private set; }

        // Constructs a suggestor with for the given node.
        public Suggestor(TrieNode<V> root)
        {
            this.overallRoot = root;
            this.root = this.overallRoot;
        }

        /*public String getCurrentPrefix()
        {
            return this.currentPrefix;
        }*/

        // Clear the current prefix because a new word is entered.
        // Percolate up to the top.
        public void clearPrefix()
        {
            this.root = this.overallRoot;
            this.currentPrefix = "";
        }

        // Percolate up to the parent node because the current node 
        // doesn't match the last character node entered.
        public void removeLastCharacter()
        {
            if (root != overallRoot)
            {
                this.root = this.root.Mom;
                // Remove last character of the current prefix.
                this.currentPrefix = this.currentPrefix.Substring(0, this.currentPrefix.Length - 1);
            }
        }


        // Return the current letter typed. 
        public Char getNewestChar()
        {
            return this.root.Key;
        }

        // Returns true if the given letter is a prefix to the current node.
        // If so, add the given letter.
        public Boolean addNextLetter(Char letter)
        {
            if (this.root.hasKid(letter))
            {
                // Add letter to the current prefix
                // Percolate down to the childe node.
                this.currentPrefix += letter;
                root = root.getKid(letter);
                return true;
            }
            return false;
        }

        // Returns true if the current prefix is an actual word in the trie.
        public Boolean prefixIsWord()
        {
            // If this node has a value, it is the last letter of a word.
            return this.root.hasValue();
        }


        // Returns the value of the word if the prefix is a word in the trie.
        public V getPrefixIfWord()
        {
            if (prefixIsWord() && this.root.Value != null)
            {
                return this.root.Value;
            }
            return null;
        }

    }
}