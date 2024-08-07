﻿using System;
using System.IO;
using System.Net.Http;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AVX;

namespace AVX.Serialization
{
    public class API
    {
        private HttpClient Client;
        public API()
        {
            // // using System.Net;
            // ServicePointManager.Expect100Continue = true;
            // ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            // // Use SecurityProtocolType.Ssl3 if needed for compatibility reasons

            this.Client = new HttpClient()
            {
                BaseAddress = new Uri("http://localhost:1769"),
            };
        }
        // app.MapGet("/{book}/{chapter}.yml", (string book, string chapter, string spec)
        public DataStream[] InsertDetails(Dictionary<string, string> settings, BookInfo book, byte c)
        {
            if (book == null || c < 1 || c > book.ChapterCount)
                return null;

            string url = book.Name + "/" + c.ToString() + ".yml";

            try
            {
                using (var awaitable = this.Client.GetAsync(url))
                {
                    awaitable.Wait();
                    var response = awaitable.Result;

                    //if (response.EnsureSuccessStatusCode())
                    {
                        var awaitableStream = response.Content.ReadAsStreamAsync();
                        awaitableStream.Wait();
                        using (TextReader content = new StreamReader(awaitableStream.Result))
                        {
                            var result = DataStream.CreateFromYaml(content, singleton: false);
                            return result.verses;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ;
            }
            return null;
        }

        // app.MapGet("/{book}/{chapter}/find/{spec}.yml", (string book, string chapter, string spec)
        // app.MapGet("/{book}/{chapter}/find-quoted/{spec}.yml", (string book, string chapter, string spec)
        public DataStream[] FindWithDetails(string spec, Dictionary<string, string> settings, BookInfo book, byte c)
        {
            if (book == null || c < 1 || c > book.ChapterCount)
                return null;

            string chapter_and_verse = book.Name + "/" + c.ToString() + "/";
            bool quoted = false;

            string normalized = spec.Trim();
        STRIPPED_QUOTES:
            if (string.IsNullOrEmpty(normalized))
                return null;

            if (normalized.StartsWith("\"") && normalized.EndsWith("\""))
            {
                if (quoted)
                    return null;
                if (normalized.Length < 3)
                    return null;
                quoted = true;

                normalized = normalized.Substring(1, normalized.Length - 2);
                goto STRIPPED_QUOTES;
            }
            string url = chapter_and_verse + (quoted ? "find-quoted/" : "find/" + normalized + ".yml");

            try
            {
                using (var awaitable = this.Client.GetAsync(url))
                {
                    awaitable.Wait();
                    var response = awaitable.Result;

                    //if (response.EnsureSuccessStatusCode())
                    {
                        var awaitableStream = response.Content.ReadAsStreamAsync();
                        awaitableStream.Wait();
                        using (TextReader content = new StreamReader(awaitableStream.Result))
                        {
                            var result = DataStream.CreateFromYaml(content, singleton:false);
                            return result.verses;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ;
            }
            return null;
        }

        // app.MapGet("/find/{spec}", (string spec) => API.api.engine.Debug_Find(spec, out message, quoted: false).ToString());
        // app.MapGet("/find-quoted/{spec}", (string spec) => API.api.engine.Debug_Find(spec, out message, quoted: true).ToString());
        public BibleResult Find(string spec, Dictionary<string,string> settings)
        {
            bool quoted = false;
            BibleResult found = new BibleResult();

            string normalized = spec.Trim();
            STRIPPED_QUOTES:
            if (string.IsNullOrEmpty(normalized))
                return found;

            if (normalized.StartsWith("\"") && normalized.EndsWith("\""))
            {
                if (quoted)
                    return found;
                if (normalized.Length < 3)
                    return found;
                quoted = true;

                normalized = normalized.Substring(1, normalized.Length-2);
                goto STRIPPED_QUOTES;
            }
            string url = (quoted ? "find-quoted/" : "find/" + normalized);

            try
            {
                byte v = 0;
                byte c = 0;
                byte b = 0;

                BookResult    book = null;
                ChapterResult chapter = null;
                VerseResult   verse = null;

                using (var awaitable = this.Client.GetAsync(url))
                {
                    awaitable.Wait();
                    var response = awaitable.Result;

                    //if (response.EnsureSuccessStatusCode())
                    {
                        var awaitableStream = response.Content.ReadAsStreamAsync();
                        awaitableStream.Wait();
                        Stream content = awaitableStream.Result;

                        for (int B = content.ReadByte(); B > 0 && B <= 66; B = content.ReadByte())
                        {
                            int C = content.ReadByte();
                            int V = content.ReadByte();

                            if (C < 1 || V < 1)
                                break;

                            if (B != b)
                            {
                                b = (byte) B;
                                book = new BookResult(b);
                                c = 0;
                                v = 0;
                                found.Add(book);
                            }
                            if (C != c)
                            {
                                c = (byte) C;
                                chapter = new ChapterResult(c);
                                v = 0;
                                book.Add(chapter);
                            }
                            if (V != v)
                            {
                                v = (byte) V;
                                verse = new VerseResult(v);
                                chapter.Add(verse);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new BibleResult();
            }
            return found;
        }
        // app.MapGet("/settings.yml", () => API.api.engine.Get_Settings());
        // app.MapGet("/settings/span",    (uint value)   => API.api.engine.Update_Settings("span", value.ToString()));
        // app.MapGet("/settings/word",    (string value) => API.api.engine.Update_Settings("word", value));
        // app.MapGet("/settings/lemma",   (string value) => API.api.engine.Update_Settings("lemma", value));
        // app.MapGet("/settings/lexicon", (string value) => API.api.engine.Update_Settings("lexicon", value));

        public Dictionary<string,string> ManageSettings()
        {
            Dictionary<string, string> items = new Dictionary<string, string>();

            string url = "settings.yml";

            try
            {
                using (var awaitable = this.Client.GetAsync(url))
                {
                    awaitable.Wait();
                    var response = awaitable.Result;

                    //if (response.EnsureSuccessStatusCode())
                    {
                        var awaitableStream = response.Content.ReadAsStreamAsync();
                        awaitableStream.Wait();
                        using (TextReader content = new StreamReader(awaitableStream.Result))
                        {
                            for (string line = content.ReadLine(); line != null; line = content.ReadLine())
                            {
                                string[] parts = line.Split(':');
                                if (parts.Length == 2)
                                {
                                    switch(parts[0].Trim().ToLower())
                                    {
                                        case "span":             items["span"]    = parts[1].Trim().ToLower(); break;
                                        case "lexicon.search":   items["lexicon"] = parts[1].Trim().ToLower(); break;
                                        case "similarity.word":  items["word"]    = parts[1].Trim().ToLower(); break;
                                        case "similarity.lemma": items["lemma"]   = parts[1].Trim().ToLower(); break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                items.Clear();
            }
            return items;
        }
        public string GetRevision()
        {
            string url = "revision.yml";

            try
            {
                using (var awaitable = this.Client.GetAsync(url))
                {
                    awaitable.Wait();
                    var response = awaitable.Result;

                    //if (response.EnsureSuccessStatusCode())
                    {
                        var awaitableStream = response.Content.ReadAsStreamAsync();
                        awaitableStream.Wait();
                        using (TextReader content = new StreamReader(awaitableStream.Result))
                        {
                            for (string line = content.ReadLine(); line != null; line = content.ReadLine())
                            {
                                string[] parts = line.Split(':');
                                if (parts.Length == 2)
                                {
                                    return parts[1].Trim();
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                ;
            }
            return string.Empty;
        }
        public Dictionary<string, string> ManageSettings(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(key))
                return ManageSettings();

            Dictionary<string, string> items = new Dictionary<string, string>();

            string url = "settings/" + key.Trim().ToLower() + "?value=" + value;

            try
            {
                using (var awaitable = this.Client.GetAsync(url))
                {
                    awaitable.Wait();
                    var response = awaitable.Result;

                    //if (response.EnsureSuccessStatusCode())
                    {
                        var awaitableStream = response.Content.ReadAsStreamAsync();
                        awaitableStream.Wait();
                        using (TextReader content = new StreamReader(awaitableStream.Result))
                        {
                            var result = content.ReadToEnd();
                            for (string line = content.ReadLine(); line != null; line = content.ReadLine())
                            {
                                string[] parts = line.Split(':');
                                if (parts.Length == 2)
                                {
                                    switch (parts[0].Trim().ToLower())
                                    {
                                        case "span": items["span"] = parts[1].Trim().ToLower(); break;
                                        case "lexicon.search": items["lexicon"] = parts[1].Trim().ToLower(); break;
                                        case "similarity.word": items["word"] = parts[1].Trim().ToLower(); break;
                                        case "similarity.lemma": items["lemma"] = parts[1].Trim().ToLower(); break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                items.Clear();
            }
            return items;
        }
    }
}
