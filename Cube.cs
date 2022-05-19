string url = "";
string id = "";
var tab = project.Tables["Данные"];

string shId = "";


while (true)
{
	lock (SyncObject)
	{
		var urls = new List<string>(File.ReadAllLines(project.Directory + @"\urls.txt").ToList());

		if (urls.Count == 0)
		{
			project.SendInfoToLog("Прошли по всем ссылкам", true);
			return "";
		}

		url = urls[0];
		urls.RemoveAt(0);
		File.WriteAllLines(project.Directory + @"\Urls.txt", urls);
	}

	project.SendInfoToLog($"Взяли url: {url}", true);


	//Запрос на страницу товара
	string get = "";
	for (int a = 0; a < 3; a++)
	{
		try
		{
			get = ZennoPoster.HTTP.Request(
					method: ZennoLab.InterfacesLibrary.Enums.Http.HttpMethod.GET,
					url: $"https://www.ozon.ru/api/composer-api.bx/page/json/v2?url={url.Replace("https://www.ozon.ru", String.Empty)}/reviews/{url}/reviews/?layout_container=pdpReviews&layout_page_index=2", Encoding: "UTF-8", UserAgent: project.Profile.UserAgent,
					respType: ZennoLab.InterfacesLibrary.Enums.Http.ResponceType.BodyOnly, Timeout:30000, proxy: "", UseOriginalUrl: true, AdditionalHeaders: new string[]
				{
					"Accept: application/json",
					"Accept-Language: ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3",
					"Accept-Encoding: gzip, deflate, br",
					"x-o3-app-name: dweb_client",
					"x-o3-app-version: release_18-4'-'2022_93b32dd5",
					"Content-Type: application/json",
					"DNT: 1",
					"Connection: keep-alive"
				}, removeDefaultHeaders: true, throwExceptionOnError: false, cookieContainer: project.Profile.CookieContainer).Replace("\\\\", String.Empty).Replace("\\", String.Empty);
			if (get.Length > 10) break;
		}
		catch
		{

		}

		Thread.Sleep(1000);
	}



	if (!Regex.IsMatch(get, "(?<=listReviewsDesktop-).*?(?=-)"))
	{
		project.SendWarningToLog("Не смогли получить ид товара", true);
		File.AppendAllText(project.Directory + @"error emails.txt", url);
		return "";
	}



	//Получаем количество товаров
	int coutComents = int.Parse(Regex.Match(get, "(?<=reviewCount\":\").*?(?=\")").Value);

	if (coutComents == 0)
	{
		project.SendInfoToLog($"У товара {url} нету отзывов", true);
		continue ;
	}

	//Получаем ид товара
	id = Regex.Match(get, "(?<=listReviewsDesktop-).*?(?=-)").Value;

	//Данные запроса
	string zap = $"{{\"url\":\"{url.Replace("https://www.ozon.ru", String.Empty)}\",\"ci\":{{\"id\":{id },\"name\":\"listReviewsDesktop\",\"vertical\":\"rpProduct\",\"version\":1,\"params\":[{{\"name\":\"paramVariantModeEnabled\"}},{{\"name\":\"paginationType\",\"text\":\"pagination\"}},{{\"name\":\"sortingType\",\"text\":\"usefulness_desc\"}},{{\"name\":\"videoAllowed\",\"bool\":true}},{{\"name\":\"paramPageSize\",\"int\":100}}]}}}}";
	string textBS = Ozon.Base64Encode(zap);

	//Проходим по страницам отзывов
	for (int page = 0; page <= coutComents / 100; page++)
	{
		string getAllComments = "";
		for (int a = 0; a < 3; a++)
		{
			getAllComments = ZennoPoster.HTTP.Request(
							method: ZennoLab.InterfacesLibrary.Enums.Http.HttpMethod.POST,
							url: "https://www.ozon.ru/api/composer-api.bx/widget/json/v2",
							content: $"{{\"asyncData\":\"{textBS}\",\"extraBody\":{{}},\"url\":\"{url.Replace("https://www.ozon.ru", String.Empty)}/reviews/?page={page}&sort=usefulness_desc\",\"componentName\":\"{url.Replace("https://www.ozon.ru", String.Empty)}/reviews/?page={page}&sort=usefulness_desc\"}}",
							contentPostingType: "application/json",
							cookieContainer: project.Profile.CookieContainer,
							respType: ZennoLab.InterfacesLibrary.Enums.Http.ResponceType.BodyOnly, Timeout:30000,
							AdditionalHeaders: new string[]
				{
					"Accept: application/json",
										"Accept-Language: ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3",
										"Accept-Encoding: gzip, deflate, br",
										"x-o3-app-name: dweb_client",
										"x-o3-app-version: release_18-4'-'2022_8a529901",
										"Origin: https://www.ozon.ru",
										"DNT: 1",
										"Connection: keep-alive"
				});
			if (getAllComments.Length > 10) break;
			Thread.Sleep(1000);
		}


		var json = JObject.Parse(getAllComments);

		
		var check = Parallel.For(0, json["state"]["reviews"].Count(), new ParallelOptions()
			{
				MaxDegreeOfParallelism = 100
			}, (numComent, b) =>
			{

				//				for (int numComent = 0; numComent < json["state"]["reviews"].Count(); numComent++)
				//				{
				string name = json["state"]["reviews"][numComent]["author"]["firstName"].ToString();
				string score = json["state"]["reviews"][numComent]["content"]["score"].ToString();//state.reviews[0].content.score
				long datePard = long.Parse(json["state"]["reviews"][numComent]["createdAt"].ToString());
				DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(datePard);

				string date = dateTimeOffset.Date.ToString("dd MMMM yyyy");

				string positive = json["state"]["reviews"][numComent]["content"]["positive"].ToString(); //state.reviews[0].content.positive
				string negative = json["state"]["reviews"][numComent]["content"]["negative"].ToString(); //state.reviews[0].content.negative
				string comment = json["state"]["reviews"][numComent]["content"]["comment"].ToString();//state.reviews[2].content.comment


				string urlsPic = "";
				//Сохраняем картинки
				for (int pick = 0; pick < (int) json["state"]["reviews"][numComent] ? ["content"] ? ["photos"].Count(); pick++)
				{
					urlsPic += json["state"]["reviews"][numComent]["content"]["photos"][pick]["url"].ToString() + Environment.NewLine;

					try
					{
						//state.reviews[2].content.photos[0].url
						using (var resp = new System.Net.Http.HttpClient().GetStreamAsync(json["state"]["reviews"][numComent]["content"]["photos"][pick]["url"].ToString()).Result)
						{
							using (var fs = new FileStream(project.Directory + @"\images\" + Regex.Match(url, "(?<=product/).*").Value + $"page-{page} numComent-{numComent} pick-{pick} {name.TrimEnd('.')}.jpg", FileMode.CreateNew))
							{
								resp.CopyToAsync(fs).Wait();
							}
						}
					}
					catch (Exception e)
					{
						project.SendInfoToLog("Ошибка сохрания картинок: " + e.Message, true);
					}


				}


				if (tab.RowCount == 0)
				{
					tab.AddRow($"url\tИмя\tРейтинг\tДата\tДостоинства\tНедостатки\tКомментарий\tСсылки картинок");
				}
				//Запись данных в таблицу
				tab.AddRow($"{url}\t{name}\t{score}\t{date}\t{positive}\t{negative}\t{comment}\t{urlsPic}");

				//}


			} );

		if (!check.IsCompleted)
		{
			Thread.Sleep(100);
		}


	}
}