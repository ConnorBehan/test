using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples
{
    /*
    public class Result
    {
        [JsonProperty(PropertyName = "$id")]
        public int vote_count { get; set; }
        public int id { get; set; }
        public bool video { get; set; }
        public double vote_average { get; set; }
        public string title { get; set; }
        public double popularity { get; set; }
        public string poster_path { get; set; }
        public string original_language { get; set; }
        public string original_title { get; set; }
        public List<int> genre_ids { get; set; }
        public string backdrop_path { get; set; }
        public bool adult { get; set; }
        public string overview { get; set; }
        public string release_date { get; set; }
        public string key { get; set; }
        public string name { get; set; }
        //public List<Result> results { get; set; }

        public string iso_639_1 { get; set; }
        public string iso_3166_1 { get; set; }

        public string site { get; set; }
        public int size { get; set; }
        public string type { get; set; }
    }

    public class Videos
    {
        public List<Videos> results { get; set; }
    }

    public class RootObject
    {
        public int page { get; set; }
        public int total_results { get; set; }
        public int total_pages { get; set; }
        public List<Result> results { get; set; }
        public int id { get; set; }
        public Videos videos { get; set; }

    }

    public class JObjects
    {
        public static string Get(object p_object)
        {
            return JsonConvert.SerializeObject(p_object);
        }
        internal static T Get<T>(string p_object)
        {
            return JsonConvert.DeserializeObject<T>(p_object);
        }
    }
}*/


    public class Genre
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class ProductionCompany
    {
        public int id { get; set; }
        public string logo_path { get; set; }
        public string name { get; set; }
        public string origin_country { get; set; }
    }

    public class ProductionCountry
    {
        public string iso_3166_1 { get; set; }
        public string name { get; set; }
    }

    public class SpokenLanguage
    {
        public string iso_639_1 { get; set; }
        public string name { get; set; }
    }

    public class Result
    {
        public string id { get; set; }
        public string iso_639_1 { get; set; }
        public string iso_3166_1 { get; set; }
        public string key { get; set; }
        public string name { get; set; }
        public string site { get; set; }
        public int size { get; set; }
        public string type { get; set; }
        public int vote_count { get; set; }
        public bool video { get; set; }
        public double vote_average { get; set; }
        public string title { get; set; }
        public double popularity { get; set; }
        public string poster_path { get; set; }
        public string original_language { get; set; }
        public string original_title { get; set; }
        public List<int> genre_ids { get; set; }
        public string backdrop_path { get; set; }
        public bool adult { get; set; }
        public string overview { get; set; }
        public string release_date { get; set; }
        public List<Result> results { get; set; }
        public string author { get; set; }
        public string content { get; set; }
        public string url { get; set; }
        public List<Listing> listings { get; set; }
    }

    public class Listing
    {
        public string title { get; set; }
        public List<string> times { get; set; }
    }

    public class Videos
    {
        public List<Result> results { get; set; }
    }

    public class RootObject
    {
        public List<Result> results { get; set; }
        public List<RootObject> resultList { get; set; }
        public bool adult { get; set; }
        public string backdrop_path { get; set; }
        public object belongs_to_collection { get; set; }
        public int budget { get; set; }
        public List<Genre> genres { get; set; }
        public string homepage { get; set; }
        public int id { get; set; }
        public string imdb_id { get; set; }
        public string original_language { get; set; }
        public string original_title { get; set; }
        public string overview { get; set; }
        public double popularity { get; set; }
        public string poster_path { get; set; }
        public List<ProductionCompany> production_companies { get; set; }
        public List<ProductionCountry> production_countries { get; set; }
        public string release_date { get; set; }
        public int revenue { get; set; }
        public int runtime { get; set; }
        public List<SpokenLanguage> spoken_languages { get; set; }
        public string status { get; set; }
        public string tagline { get; set; }
        public string title { get; set; }
        public bool video { get; set; }
        public double vote_average { get; set; }
        public int vote_count { get; set; }
        public Videos videos { get; set; }

        public int total_results { get; set; }
        public int total_pages { get; set; }

        public string venue_id { get; set; }
        public string name { get; set; }
        public string address { get; set; }
        public string url { get; set; }
        public string distance { get; set; }
        public string link { get; set; }
        public List<string> time { get; set; }
    }
}