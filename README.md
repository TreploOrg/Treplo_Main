# Treplo

## Описание ##
Изначально задумывался как Discord-бот для прослушивания музыки на замену Rythm и других ботов, что перестали поддерживать воспроизведение роликов с YouTube. Сейчас разделился на три составляющие:

### Поисковой сервис ###
Выполняет поиск по запросу в разных сервисах вроде YouTube, Soundcloud и т.п. (на данный момент работает только с YouTube)

### Плеер ###
Хранит текущий плейлист и проигрывает запрошенное аудио в запрошенном формате

### Фронт для взаимодействия с Discord ###
Получает команды от Discord'а и проигрывает аудио в канал, в котором был вызван

## Использованные технологии ##
Asp.net - веб-фреймворк  
Discord.net - для связи с дискордом  
Orleans - реализация модели актора  
ffmpeg - обработка аудио  

## Установка ##
#### Локальный запуск ####
1) Скачать проект
2) Поместите [ffmpeg](https://www.gyan.dev/ffmpeg/builds/) в папку Treplo\external перед сборкой, или напрямую в корень проекта после сборки
3) Запустить проект на локальной машине
#### Использование бота ####
4) Пригласить бота на сервер в Discord
5) Пользоваться

## Планы ##
В планах поиск в spotify, soundcloud, yandex music, а также создание собственных плейлистов и воспроизведение локальных файлов

## Взаимодействие между сервисами ##
![interactions](https://github.com/TreploOrg/Treplo_Main/assets/81422677/8d23592e-a7e8-44c3-85a9-e53da59df5af)

## Роадмап ##
![roadmap](https://user-images.githubusercontent.com/81422717/215155762-890e10bc-4319-4873-b473-7bd2ca4c1b66.png)
