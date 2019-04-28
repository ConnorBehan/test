// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Xml.Linq;

//dispatch refresh --bot c:\Users\conor\Desktop\MovieBot2.bot --secret KhP+l5f04+k7myOTdy/KwU3fHyqOjWIrhzyBT0FmfY4=

namespace Microsoft.BotBuilderSamples
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each interaction from the user, an instance of this class is called.
    /// This is a Transient lifetime service. Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single Turn, should be carefully managed.
    /// </summary>
    public class CinemaBot : IBot
    {

        // Supported LUIS Intents
        public const string GreetingIntent = "Greeting";
        public const string CancelIntent = "Cancel";
        public const string HelpIntent = "Help";
        public const string NoneIntent = "None";

        public const string BookTicket = "MovieTickets_Book";
        public const string WatchTrailer = "Trailer";

        public const string ViewMovies = "NewMovies";
        public const string PopularMovies = "PopularMovies";
        public const string UpcomingMovies = "UpcomingMovies";
        public const string MovieTimes = "MovieTimes";
        public const string ShowMovieTimes = "ShowMovieTimes";
        public const string MovieID = "MovieID";

        public const string ActionMovies = "ActionMovies";
        public const string HorrorMovies = "HorrorMovies";
        public const string ComedyMovies = "ComedyMovies";
        public const string RomanceMovies = "RomanceMovies";
        public const string FamilyMovies = "FamilyMovies";

        private const string WelcomeText = "How are you today?";

        /// Key in the Bot config (.bot file) for the Luis instance.
        //private const string MovieLuisKey = "l_BasicBotLuisApplication"; // BasicBotLuisApplication or MovieBot2-82b0
        private const string MovieLuisKey = "MovieBot2-82b0"; // BasicBotLuisApplication or MovieBot2-82b0

        /// Key in the Bot config (.bot file) for the Dispatch.
        private const string DispatchKey = "MovieBot2Dispatch";

        /// Key in the Bot config (.bot file) for the QnaMaker instance.
        /// In the .bot file, multiple instances of QnaMaker can be configured.
        private const string QnAMakerKey = "CinemaSampleKB";

        /// Services configured from the ".bot" file.
        private readonly BotServices _services;

        /// Initializes a new instance of the <see cref="CinemaBot"/> class.
        /// </summary>
        /// <param name="services">Services configured from the ".bot" file.</param>
        public CinemaBot(BotServices services)
        {
            _services = services ?? throw new System.ArgumentNullException(nameof(services));

            if (!_services.QnAServices.ContainsKey(QnAMakerKey))
            {
                throw new System.ArgumentException($"Invalid configuration. Please check your '.bot' file for a QnA service named '{DispatchKey}'.");
            }

            if (!_services.LuisServices.ContainsKey(MovieLuisKey))
            {
                throw new System.ArgumentException($"Invalid configuration. Please check your '.bot' file for a Luis service named '{MovieLuisKey}'.");
            }
        }

        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response, with no stateful conversation.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext.Activity.Type == ActivityTypes.Message && !turnContext.Responded)
            {
                // Get the intent recognition result
                var recognizerResult = await _services.LuisServices[DispatchKey].RecognizeAsync(turnContext, cancellationToken);
                var topIntent = recognizerResult?.GetTopScoringIntent();

                if (topIntent == null)
                {
                    await turnContext.SendActivityAsync("Unable to get the top intent.");
                }
                else
                {
                    await DispatchToTopIntentAsync(turnContext, topIntent, cancellationToken);
                }
            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                // Send a welcome message to the user and tell them what actions they may perform to use this bot
                if (turnContext.Activity.MembersAdded != null)
                {
                    await WelcomeMessage(turnContext, cancellationToken);
                }
            }
            else
            {
                await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected", cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Depending on the intent from Dispatch, routes to the right LUIS model or QnA service.
        /// </summary>
        private async Task DispatchToTopIntentAsync(ITurnContext context, (string intent, double score)? topIntent, CancellationToken cancellationToken = default(CancellationToken))
        {
            //const string LuisDispatchKey = "l_BasicBotLuisApplication";
            const string LuisDispatchKey = "l_MovieBot2-82b0";
            const string noneDispatchKey = "None";
            const string qnaDispatchKey = "q_CinemaSampleKB";

            switch (topIntent.Value.intent)
            {
                case LuisDispatchKey:
                    await DispatchToLuisModelAsync(context, MovieLuisKey);

                    var luisResponse = await _services.LuisServices[MovieLuisKey].RecognizeAsync(context, cancellationToken);

                    if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "greeting")
                    {
                        await WelcomeMessage(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "newmovies")
                    {
                        await FetchNewMoviesAsync(context, cancellationToken); //waits for reply
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "trailer")
                    {
                        await FetchNewTrailersAsync(context, cancellationToken); //waits for reply
                        await FetchNewTrailerSecondCallsAsync(context, cancellationToken); //waits for reply
                        await FetchNewTrailerThirdCallAsync(context, cancellationToken); //waits for reply
                        await FetchNewTrailerFourthCallAsync(context, cancellationToken); //waits for reply
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "popularmovies")
                    {
                        await ShowPopularMovies(context, cancellationToken); //waits for reply ShowPopularMovies
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "upcomingmovies")
                    {
                        await ShowUpcomingMovies(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "moviegenres")
                    {
                        await DisplayMovieGenres(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "actionmovies")
                    {
                        await ShowActionMovies(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "horrormovies")
                    {
                        await ShowHorrorMovies(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "comedymovies")
                    {
                        await ShowComedyMovies(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "romancemovies")
                    {
                        await ShowRomanceMovies(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "familymovies")
                    {
                        await ShowFamilyMovies(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "movietickets_book")
                    {
                        await context.SendActivityAsync("Select your Cinema below: ");
                        await FindMovieTimes(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "movietimes")
                    {
                        await FindMovieTimes(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "movieid")
                    {
                        await MovieReviews(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "showmovietimes")
                    {
                        await ShowTimes(context, cancellationToken);
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "help")
                    {
                        await context.SendActivityAsync($"Let's start again.");
                        await WelcomeMessage(context, cancellationToken); 
                    }

                    else if (luisResponse.Intents.FirstOrDefault().Key.ToLower() == "none")
                    {
                        await context.SendActivityAsync($"Sorry, I can't help with that. Try again.");
                        //await WelcomeMessage(context, cancellationToken);
                    }
                    break; 

                /*case noneDispatchKey:
                    //  None intent (none of the above).
                    await context.SendActivityAsync($"Let's start again.");
                    await WelcomeMessage(context, cancellationToken);
                    break;
                    */
                case qnaDispatchKey:
                    await DispatchToQnAMakerAsync(context, QnAMakerKey);
                    break;

                default:
                    // The intent didn't match any case, so just display the recognition results.
                    await context.SendActivityAsync($"Dispatch intent: {topIntent.Value.intent} ({topIntent.Value.score}).");

                    break;
            }
        }

        /// <summary>
        /// Dispatches the turn to the request QnAMaker app.
        /// </summary>
        private async Task DispatchToQnAMakerAsync(ITurnContext context, string appName, CancellationToken cancellationToken = default(CancellationToken))
        {
            var results = await _services.QnAServices[appName].GetAnswersAsync(context).ConfigureAwait(false);
            if (results.Any())
            {
                // Create an attachment.
                var attachment = new Attachment
                {
                    ContentUrl = "imageUrl.png",
                    ContentType = "greeting/jpg",
                    Name = "imageName",
                };
                var reply = context.Activity.CreateReply();
                // Add the attachment to our reply.
                reply.Attachments = new List<Attachment>() { attachment };

                await context.SendActivityAsync(results.First().Answer, cancellationToken: cancellationToken);
            }
            else
            {
                await context.SendActivityAsync($"Sorry, I don't understand. Please try rephrasing your message.");
            }
        }

        /// <summary>
        /// Dispatches the turn to the requested LUIS model.
        /// </summary>
        private async Task DispatchToLuisModelAsync(ITurnContext context, string appName, CancellationToken cancellationToken = default(CancellationToken))
        {
            //await context.SendActivityAsync($"Sending your request to CinemaBot ...");
            var result = await _services.LuisServices[appName].RecognizeAsync(context, cancellationToken);

            //await context.SendActivityAsync($"Intents detected by the CinemaBot app:\n\n{string.Join("\n\n", result.Intents)}");

            /*if (result.Entities.Count > 0)
            {
                await context.SendActivityAsync($"The following entities were found in the message:\n\n{string.Join("\n\n", result.Entities)}");
            }*/
        }

        private static async Task WelcomeMessage(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            var response = context.Activity.CreateReply();

            // Create a HeroCard with options for the user to choose to interact with the bot.
            var card = new HeroCard();
            card.Title = "Welcome to CinemaBot!";
            card.Text = @"You can watch trailers, view movies (new,upcoming), times, or movie genres.";
            card.Images = new List<CardImage>() { new CardImage("https://lajoyalink.com/wp-content/uploads/2018/03/Movie.jpg") };
            card.Buttons = new List<CardAction>()
            {
                new CardAction(ActionTypes.ImBack, title: "View Movies", value: "View Movies"),
                new CardAction(ActionTypes.ImBack, title: "Watch Trailers", value: "Watch Trailers"),
                new CardAction(ActionTypes.ImBack, title: "Show Times", value: "Show Times"),
            };

            // Add the attachment to our reply.
            response.Attachments = new List<Attachment>() { card.ToAttachment() };
            await context.SendActivityAsync(response, cancellationToken);
        }

        private static async Task FetchNewMoviesAsync(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/movie/now_playing?api_key=18d87ad4551a3f446111ac081339203c&language=en-US&page=1";
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            /*var heroCard = new HeroCard
                            {
                                Title = result.results[i].title,
                                Subtitle = "Rating: " + result.results[i].vote_average + "\n" + result.results[i].release_date,
                                Text = result.results[i].overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) },
                                Buttons = new List<CardAction> { new CardAction(ActionTypes.ImBack, "Reviews:", value: "Movie ID " + result.results[i].id) },
                            };*/

                            var card = new HeroCard();
                            card.Title = result.results[i].title;
                            card.Subtitle = "Rating: " + result.results[i].vote_average + "/10 " + "\nRelease Date: " + result.results[i].release_date;
                            //card.Text = result.results[i].overview;
                            card.Images = new List<CardImage>() { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) };
                            card.Buttons = new List<CardAction>()
                            {
                                new CardAction(ActionTypes.ImBack, "Read Review:", value: "Movie ID " + result.results[i].id),
                            };

                            // Add the attachment to our reply.
                            reply.Attachments.Add(card.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }

            catch (Exception ex)
            {
                //throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task FetchNewTrailersAsync(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieIDs = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/movie/popular?api_key=18d87ad4551a3f446111ac081339203c&language=en-US&page=1"; //"http://api.themoviedb.org/3/movie/157336/videos?api_key=18d87ad4551a3f446111ac081339203c";
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            MovieIDs.Add(result.results[i]);
                        }
                    }
                }

                using (HttpClient client = new HttpClient())
                {
                    for (int i = 0; i < MovieIDs.Count(); i++)
                    {
                        //Assuming that the api takes the user message as a query paramater
                        string RequestURI = "https://api.themoviedb.org/3/movie/" + MovieIDs[i].id + "?api_key=18d87ad4551a3f446111ac081339203c&append_to_response=videos"; //"http://api.themoviedb.org/3/movie/157336/videos?api_key=18d87ad4551a3f446111ac081339203c";
                        HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);

                        if (responsemMsg.IsSuccessStatusCode)
                        {
                            var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                            RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                            var reply = context.Activity.CreateReply();

                            var heroCard = new HeroCard
                            {
                                Title = result.original_title,
                                Subtitle = "Release date: " + result.release_date + "\nRuntime: " + result.runtime + " minutes",
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.poster_path) },
                                Buttons = new List<CardAction> {
                                    new CardAction(ActionTypes.PlayVideo, "Watch Trailer", value: "https://www.youtube.com/watch?v=" + result.videos.results[i].key),
                                    new CardAction(ActionTypes.OpenUrl, title: "IMDB", value: "https://www.imdb.com/title/" + result.imdb_id),
                                },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments = new List<Attachment>() { heroCard.ToAttachment() };

                            await context.SendActivityAsync(reply);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task FetchNewTrailerSecondCallsAsync(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieIDs = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/movie/popular?api_key=18d87ad4551a3f446111ac081339203c&language=en-US&page=1"; //"http://api.themoviedb.org/3/movie/157336/videos?api_key=18d87ad4551a3f446111ac081339203c";
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            MovieIDs.Add(result.results[i]);
                        }
                    }
                }

                using (HttpClient client = new HttpClient())
                {
                    for (int i = 0; i < MovieIDs.Count(); i++)
                    {
                        //Assuming that the api takes the user message as a query paramater
                        string RequestURI = "https://api.themoviedb.org/3/movie/" + MovieIDs[i + 3].id + "?api_key=18d87ad4551a3f446111ac081339203c&append_to_response=videos"; //"http://api.themoviedb.org/3/movie/157336/videos?api_key=18d87ad4551a3f446111ac081339203c";
                        HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);

                        if (responsemMsg.IsSuccessStatusCode)
                        {
                            var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                            RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                            var reply = context.Activity.CreateReply();

                            var heroCard = new HeroCard
                            {
                                Title = result.original_title,
                                Subtitle = "Release date: " + result.release_date + "\nRuntime: " + result.runtime + " minutes",
                                //Text = result.overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.poster_path) },
                                Buttons = new List<CardAction> {
                                new CardAction(ActionTypes.PlayVideo, "Watch Trailer", value: "https://www.youtube.com/watch?v=" + result.videos.results[i].key),
                                new CardAction(ActionTypes.OpenUrl, title: "IMDB", value: "https://www.imdb.com/title/" + result.imdb_id),
                                },
                            };

                                /*
                                Title = result.original_title,
                                Subtitle = "Release date: " + result.release_date + "\nRuntime: " + result.runtime + " minutes" + "\n" + result.overview,
                                //Text = result.overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.poster_path) },
                                Buttons = new List<CardAction> {
                                    new CardAction(ActionTypes.PlayVideo, "Watch Trailer", value: "https://www.youtube.com/watch?v=" + result.videos.results[i].key),
                                    new CardAction(ActionTypes.OpenUrl, title: "IMDB", value: "https://www.imdb.com/title/" + result.imdb_id),
                                },
                            };*/

                                // Add the attachment to our reply.
                                reply.Attachments = new List<Attachment>() { heroCard.ToAttachment() };

                            await context.SendActivityAsync(reply);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task FetchNewTrailerThirdCallAsync(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieIDs = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/movie/popular?api_key=18d87ad4551a3f446111ac081339203c&language=en-US&page=1"; //"http://api.themoviedb.org/3/movie/157336/videos?api_key=18d87ad4551a3f446111ac081339203c";
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            MovieIDs.Add(result.results[i]);
                        }
                    }
                }

                using (HttpClient client = new HttpClient())
                {
                    for (int i = 0; i < MovieIDs.Count(); i++)
                    {
                        //Assuming that the api takes the user message as a query paramater
                        string RequestURI = "https://api.themoviedb.org/3/movie/" + MovieIDs[i + 6].id + "?api_key=18d87ad4551a3f446111ac081339203c&append_to_response=videos"; //"http://api.themoviedb.org/3/movie/157336/videos?api_key=18d87ad4551a3f446111ac081339203c";
                        HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                        
                        if (responsemMsg.IsSuccessStatusCode)
                        {
                            var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                            RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                            var reply = context.Activity.CreateReply();

                            var heroCard = new HeroCard
                            {
                                Title = result.original_title,
                                Subtitle = "Release date: " + result.release_date + "\nRuntime: " + result.runtime + " minutes",
                                //Text = result.overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.poster_path) },
                                Buttons = new List<CardAction> { new CardAction(ActionTypes.PlayVideo, "Watch Trailer", value: "https://www.youtube.com/watch?v=" + result.videos.results[i].key),
                                new CardAction(ActionTypes.OpenUrl, title: "IMDB", value: "https://www.imdb.com/title/" + result.imdb_id)
                                },
                            };

                        // Add the attachment to our reply.
                        reply.Attachments = new List<Attachment>() { heroCard.ToAttachment() };

                        await context.SendActivityAsync(reply);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
               // throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task FetchNewTrailerFourthCallAsync(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieIDs = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/movie/popular?api_key=18d87ad4551a3f446111ac081339203c&language=en-US&page=1"; //"http://api.themoviedb.org/3/movie/157336/videos?api_key=18d87ad4551a3f446111ac081339203c";
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            MovieIDs.Add(result.results[i]);
                        }
                    }
                }

                using (HttpClient client = new HttpClient())
                {
                    for (int i = 0; i < MovieIDs.Count(); i++)
                    {
                        //Assuming that the api takes the user message as a query paramater
                        string RequestURI = "https://api.themoviedb.org/3/movie/" + MovieIDs[i + 9].id + "?api_key=18d87ad4551a3f446111ac081339203c&append_to_response=videos"; //"http://api.themoviedb.org/3/movie/157336/videos?api_key=18d87ad4551a3f446111ac081339203c";
                        HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);

                        if (responsemMsg.IsSuccessStatusCode)
                        {
                            var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                            RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                            var reply = context.Activity.CreateReply();

                            var heroCard = new HeroCard
                            {
                                Title = result.original_title,
                                Subtitle = "Release date: " + result.release_date + "\nRuntime: " + result.runtime + " minutes",
                                //Text = result.overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.poster_path) },
                                Buttons = new List<CardAction> { new CardAction(ActionTypes.PlayVideo, "Watch Trailer", value: "https://www.youtube.com/watch?v=" + result.videos.results[i].key),
                                new CardAction(ActionTypes.OpenUrl, title: "IMDB", value: "https://www.imdb.com/title/" + result.imdb_id)
                                },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments = new List<Attachment>() { heroCard.ToAttachment() };

                            await context.SendActivityAsync(reply);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task ShowPopularMovies(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/movie/popular?api_key=18d87ad4551a3f446111ac081339203c&language=en-US&page=1";
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            var heroCard = new HeroCard
                            {
                                Title = result.results[i].title,
                                Subtitle = "Rating: " + result.results[i].vote_average + "/10" + "\nRelease Date: " + result.results[i].release_date,
                                //Text = result.results[i].overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments.Add(heroCard.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }

            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task ShowUpcomingMovies(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/movie/upcoming?api_key=18d87ad4551a3f446111ac081339203c&language=en-US&page=1";
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            var heroCard = new HeroCard
                            {
                                Title = result.results[i].title,
                                Subtitle = "Rating: " + result.results[i].vote_average + "/10" + "\nRelease Date: " + result.results[i].release_date,
                                Text = result.results[i].overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments.Add(heroCard.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }

            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task MovieReviews(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Substring to get venue ID
                    var startIndex = "Movie ID ".Length;
                    var movieID = context.Activity.Text.Substring(startIndex);

                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/movie/" + movieID + "/reviews?api_key=18d87ad4551a3f446111ac081339203c&append_to_response=videos";
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < 3; i++)
                        {
                            /*var heroCard = new HeroCard
                            {
                                Title = result.results[i].author,
                                Subtitle = result.results[i].content.PadRight(300).Substring(0, 140).TrimEnd() + "...",
                            };*/
                            if (result.total_results == 0)
                            {
                                await context.SendActivityAsync($"Sorry, no reviews have been submitted yet " );
                            }

                            if (result.results[i].author.Length > 1)
                            {
                                await context.SendActivityAsync("User: " +  result.results[i].author + "\n\n" + result.results[i].content); //.PadRight(500).Substring(0, 340).TrimEnd() + "...");
                            }
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task ShowActionMovies(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieIDs = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/discover/movie?api_key=18d87ad4551a3f446111ac081339203c&with_genres=28"; //Action Movies
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            var heroCard = new HeroCard
                            {
                                Title = result.results[i].title,
                                Subtitle = "Rating: " + result.results[i].vote_average + "/10" + "\nRelease Date: " + result.results[i].release_date,
                                //Text = result.results[i].overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments.Add(heroCard.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                } 
            }
            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task ShowHorrorMovies(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieIDs = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/discover/movie?api_key=18d87ad4551a3f446111ac081339203c&with_genres=27"; //Horror Movies
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            var heroCard = new HeroCard
                            {
                                Title = result.results[i].title,
                                Subtitle = "Rating: " + result.results[i].vote_average + "/10" + "\nRelease Date: " + result.results[i].release_date,
                                //Text = result.results[i].overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments.Add(heroCard.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task ShowComedyMovies(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieIDs = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/discover/movie?api_key=18d87ad4551a3f446111ac081339203c&with_genres=35"; //Action Movies
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            var heroCard = new HeroCard
                            {
                                Title = result.results[i].title,
                                Subtitle = "Rating: " + result.results[i].vote_average + "/10" + "\nRelease Date: " + result.results[i].release_date,
                                //Text = result.results[i].overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments.Add(heroCard.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task ShowRomanceMovies(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieIDs = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/discover/movie?api_key=18d87ad4551a3f446111ac081339203c&with_genres=10749"; //Action Movies
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            var heroCard = new HeroCard
                            {
                                Title = result.results[i].title,
                                Subtitle = "Rating: " + result.results[i].vote_average + "/10" + "\nRelease Date: " + result.results[i].release_date,
                                //Text = result.results[i].overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments.Add(heroCard.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task ShowFamilyMovies(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieIDs = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/discover/movie?api_key=18d87ad4551a3f446111ac081339203c&with_genres=10751"; //Action Movies
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            var heroCard = new HeroCard
                            {
                                Title = result.results[i].title,
                                Subtitle = "Rating: " + result.results[i].vote_average + "/10" + "\nRelease Date: " + result.results[i].release_date,
                                //Text = result.results[i].overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments.Add(heroCard.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task DisplayMovieGenres(ITurnContext turnContext, CancellationToken cancellationToken)
        {
                    var reply = turnContext.Activity.CreateReply();

                    // Create an attachment.
                    var attachment = new Attachment
                    {
                        ContentUrl = "https://www.filmsite.org/images/filmgenres.jpg",
                        ContentType = "image/jpg",
                        Name = "movie",
                    };

                    // Add the attachment to our reply.
                    //reply.Attachments = new List<Attachment>() { attachment };

                    // Create a HeroCard with options for the user to choose to interact with the bot.
                    var card = new HeroCard
                    {
                        Images = new List<CardImage> { new CardImage("https://www.filmsite.org/images/filmgenres.jpg") },
                        Text = "Select a genre:",
                        Buttons = new List<CardAction>()
                        {
                            new CardAction(ActionTypes.ImBack, title: "Action", value: "Action"),
                            new CardAction(ActionTypes.ImBack, title: "Comedy", value: "Comedy"),
                            new CardAction(ActionTypes.ImBack, title: "Horror", value: "Horror"),
                            new CardAction(ActionTypes.ImBack, title: "Romance", value: "Romance"),
                            new CardAction(ActionTypes.ImBack, title: "Family", value: "Family"),
                        },
                    };


                    // Add the card to our reply.
                    reply.Attachments = new List<Attachment>() { card.ToAttachment() };

                    await turnContext.SendActivityAsync(reply, cancellationToken);
        }

        private static async Task FindMovieTimes(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "http://moviesapi.herokuapp.com/cinemas/find/:Dublin"; // + context.Activity.Text;
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();
                        RootObject[] result;
                        result = JsonConvert.DeserializeObject<RootObject[]>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 1; i < 6; i++) //changed guard from 0 to 1, tallaght api no longer working
                        {
                            string url;

                            if(result[i].url.Length > 7)
                            {
                                url = result[i].url;
                            }

                            else
                            {
                                url = "";
                            }
                            var heroCard = new HeroCard
                            {
                                Title = result[i].name,
                                Subtitle = "Address: " + result[i].address + "\n" + url,
                                Buttons = new List<CardAction> { new CardAction(ActionTypes.ImBack, "Show times", value: "Cinema ID: " + result[i].venue_id)
                                },
                            };
                            // Add the attachment to our reply.
                            reply.Attachments.Add(heroCard.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }

            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        private static async Task ShowTimes(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                   //Substring to get venue ID
                    var startIndex = "Cinema ID: ".Length;
                    var venueID = context.Activity.Text.Substring(startIndex); // 123

                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "http://moviesapi.herokuapp.com/cinemas/"  + venueID + "/showings"; 
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();
                        RootObject[] result;
                        result = JsonConvert.DeserializeObject<RootObject[]>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.Count(); i++)
                        {

                            string movieTimes = string.Join(", ", result[i].time.ToList());

                            /*if (venueID == "10585") //code for point cinema
                            {
                                var heroCard = new HeroCard
                            {
                                Title = result[i].title,
                                Subtitle = "Times: " + movieTimes,                     
                                Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.odeoncinemas.ie/cinemas/point_square/209/") },
                            };
                                reply.Attachments.Add(heroCard.ToAttachment());
                            }*/

                             if (venueID == "7513") // code for cineworld
                            {
                                var heroCard = new HeroCard
                                {
                                    Title = result[i].title,
                                    Subtitle = "Times: " + movieTimes,
                                    Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.cineworld.ie/cinemas/dublin/") },
                                };

                                reply.Attachments.Add(heroCard.ToAttachment());
                            }

                            else if (venueID == "9690")//odeon coolock
                            {
                                var heroCard = new HeroCard
                                {
                                    Title = result[i].title,
                                    Subtitle = "Times: " + movieTimes,
                                    Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.odeoncinemas.ie/cinemas/coolock/23/") },
                                };

                                reply.Attachments.Add(heroCard.ToAttachment());
                            }

                            else if (venueID == "9698")//odeon stillorgan
                            {
                                var heroCard = new HeroCard
                                {
                                    Title = result[i].title,
                                    Subtitle = "Times: " + movieTimes,
                                    Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.odeoncinemas.ie/cinemas/stillorgan/207/") },
                                };

                                reply.Attachments.Add(heroCard.ToAttachment());
                            }

                            else if (venueID == "10181")//odeon charlestown
                            {
                                var heroCard = new HeroCard
                                {
                                    Title = result[i].title,
                                    Subtitle = "Times: " + movieTimes,
                                    Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.odeoncinemas.ie/cinemas/charlestown/220/") },
                                };

                                reply.Attachments.Add(heroCard.ToAttachment());
                            }

                            else if (venueID == "9352")//vue liffey valley
                            {
                                var heroCard = new HeroCard
                                {
                                    Title = result[i].title,
                                    Subtitle = "Times: " + movieTimes,
                                    Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.myvue.com/cinema/dublin/film/" + result[i].title.Replace(" ", "-").Replace(":", "-").PadRight(20).Substring(0, 20).TrimEnd()) },
                                };

                                reply.Attachments.Add(heroCard.ToAttachment());
                            }

                            else if (venueID == "9691")//odeon blanch
                            {
                                var heroCard = new HeroCard
                                {
                                    Title = result[i].title,
                                    Subtitle = "Times: " + movieTimes,
                                    Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.odeoncinemas.ie/cinemas/blanchardstown/25/") },
                                };

                                reply.Attachments.Add(heroCard.ToAttachment());
                            }

                            else if (venueID == "10698")//vue ashbourne
                            {
                                var heroCard = new HeroCard
                                {
                                    Title = result[i].title,
                                    Subtitle = "Times: " + movieTimes,
                                    Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.myvue.com/cinema/ashbourne/film/" + result[i].title.Replace(" ", "-").PadRight(20).Substring(0, 20).TrimEnd()) },
                                };

                                reply.Attachments.Add(heroCard.ToAttachment());
                            }

                            else if (venueID == "9696")//odeon naas
                            {
                                var heroCard = new HeroCard
                                {
                                    Title = result[i].title,
                                    Subtitle = "Times: " + movieTimes,
                                    Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.odeoncinemas.ie/cinemas/naas/204/") },
                                };

                                reply.Attachments.Add(heroCard.ToAttachment());
                            }

                            else if (venueID == "9696")//odeon newbridge
                            {
                                var heroCard = new HeroCard
                                {
                                    Title = result[i].title,
                                    Subtitle = "Times: " + movieTimes,
                                    Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, title: "Book", value: "https://www.odeoncinemas.ie/cinemas/newbridge/160/") },
                                };

                                reply.Attachments.Add(heroCard.ToAttachment());
                            }
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again, say 'Hi' \n" + ex);
            }
        }

        /*private static async Task SelectMovieToSearch(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/movie/now_playing?api_key=18d87ad4551a3f446111ac081339203c&language=en-US&page=1";
                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);
                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };

                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            var card = new HeroCard();
                            card.Title = result.results[i].title;
                            card.Images = new List<CardImage>() { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) };
                            card.Buttons = new List<CardAction>()
                            {
                                new CardAction(ActionTypes.ImBack, "More info:", value: "Movie ID " + result.results[i].id),
                            };

                            // Add the attachment to our reply.
                            reply.Attachments.Add(card.ToAttachment());
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                        await SearchMovie(context, cancellationToken);
                    }
                }
            }

            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again" + ex);
            }
        }

        private static async Task SearchMovie(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Result> MovieTitles = new List<Result>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Substring to get venue ID
                    var startIndex = "Movie ID ".Length;
                    var movieID = context.Activity.Text.Substring(startIndex); // 123

                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.themoviedb.org/3/search/movie?api_key=###&query=" + movieID;
                        HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);

                        if (responsemMsg.IsSuccessStatusCode)
                        {
                            var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                            RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                            var reply = context.Activity.CreateReply();

                            var heroCard = new HeroCard
                            {
                                Title = result.original_title,
                                Subtitle = "Release date: " + result.release_date + "\nRuntime: " + result.runtime + " minutes" + "\n" + result.overview,
                                //Text = result.overview,
                                Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.poster_path) },
                                Buttons = new List<CardAction> {
                                    new CardAction(ActionTypes.OpenUrl, title: "IMDB", value: "https://www.imdb.com/title/" + result.imdb_id),
                                },
                            };

                            // Add the attachment to our reply.
                            reply.Attachments = new List<Attachment>() { heroCard.ToAttachment() };

                            await context.SendActivityAsync(reply + "\n" + result.overview);
                        }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Oops, CinemaBot has run into some trouble, let's start again" + ex);
            }
        }*/

        /*private static async Task FindMovieTimes(ITurnContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Assuming that the api takes the user message as a query paramater
                    string RequestURI = "https://api.cinelist.co.uk/get/times/many/10681"; // http://moviesapi.herokuapp.com/cinemas/find/:DUBLIN

                    HttpResponseMessage responsemMsg = await client.GetAsync(RequestURI);

                    if (responsemMsg.IsSuccessStatusCode)
                    {
                        var apiResponse = await responsemMsg.Content.ReadAsStringAsync();

                        RootObject result = JsonConvert.DeserializeObject<RootObject>(apiResponse);

                        var card = new HeroCard { };

                        List<CardImage> cardImages = new List<CardImage>();
                        var reply = context.Activity.CreateReply();
                        reply.Attachments = new List<Attachment>() { };
                 
                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            for (int x = 0; x < result.results[i].listings.Count(); x++) 
                            {
                                string movieTimes = string.Join(", ", result.results[i].listings[x].times.ToList());

                                var heroCard = new HeroCard
                                {
                                    Title = result.results[i].listings[x].title,
                                    //Subtitle = "Time: " + result.results[i].listings[x].times,
                                    Text = movieTimes, 
                                    //Images = new List<CardImage> { new CardImage("https://image.tmdb.org/t/p/original" + result.results[i].poster_path) },
                                    //Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, "Book", value: "https://www.odeoncinemas.ie/cinema-tickets") },
                                };

                                // Add the attachment to our reply.
                                reply.Attachments.Add(heroCard.ToAttachment());
                            }
                        }
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        await context.SendActivityAsync(reply);
                    }
                }
            }

            catch (Exception ex)
            {

            }
        }*/

    }
}

        /*

            // Let's iterate the first few hits
            foreach (SearchMovie result in results.Results.Take(3))
            {
                // Print out each hit
                Console.WriteLine(result.Id + ": " + result.Title);
                Console.WriteLine("\t Original Title: " + result.OriginalTitle);
                Console.WriteLine("\t Release date  : " + result.ReleaseDate);
                Console.WriteLine("\t Popularity    : " + result.Popularity);
                Console.WriteLine("\t Vote Average  : " + result.VoteAverage);
                Console.WriteLine("\t Vote Count    : " + result.VoteCount);
                Console.WriteLine();
                Console.WriteLine("\t Backdrop Path : " + result.BackdropPath);
                Console.WriteLine("\t Poster Path   : " + result.PosterPath);

                Console.WriteLine();
            }

           private static Attachment GetMovieCard(string movie_title, string year, string runtime, string genre, string plot, string language, string poster, string rating, string imdb_title, string rtURL)
        {
            var movieCard = new ThumbnailCard
            {
                Title = movie_title,
                Subtitle = "Genre: " + genre +  " Year: " + year + " Runtime: " + runtime + " Language(s): " + language + " IMDb Rating: " + rating,
                Text = plot,
                Images = new List<CardImage> { new CardImage(url: poster) },
                Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, "See on IMDb", value: "http://www.imdb.com/title/" + imdb_title),
                                                 new CardAction(ActionTypes.OpenUrl, "See on Rotten Tomatoes", value: rtURL) }
            };

            return movieCard.ToAttachment();
        }
        }
    }
}

/*private async Task<string> SearchMovie(string title)
        {
            string response = string.Empty;
            var movieSearch = await Repository.MovieRepository.Search(title);

            if (movieSearch?.Search?.Count() > 0)
            {
                for (int index = 0; index < movieSearch.Search.Count(); index++)
                {
                    response += Utility.NewLine + $"{movieSearch.Search[index].Title}";
                }

                response += Utility.NewLine + $"***Do you mean any of the above {movieSearch.Search.Count()}, If yes please type the title again.***";
            }
            else
            {
                response = "I am really sorry,I don't know the details of the movie you are looking for :( . Please give me a chance by trying again.";
                response = PublicMessages.NoInformationMessage;
            }
            return response;
        } */
