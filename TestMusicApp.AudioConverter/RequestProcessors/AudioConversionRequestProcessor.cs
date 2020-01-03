﻿using System;
using System.IO;
using System.Threading.Tasks;
using TestMusicApp.AudioConverter.MessageBrokers;
using TestMusicApp.AudioConverter.Services;
using TestMusicApp.AudioConverter.Storages;

namespace TestMusicApp.AudioConverter.RequestProcessors
{
    public class AudioConversionRequestProcessor : IAudioConversionRequestProcessor
    {
        private readonly IAudioConversionService _audioConversionService;
        private readonly IAudioStorage _audioStorage;
        private readonly IAudioConversionMessageBroker _audioConversionMessageBroker;

        public AudioConversionRequestProcessor(
            IAudioConversionService audioConversionService,
            IAudioStorage audioStorage,
            IAudioConversionMessageBroker audioConversionMessageBroker
        )
        {
            this._audioConversionService = audioConversionService;
            this._audioStorage = audioStorage;
            this._audioConversionMessageBroker = audioConversionMessageBroker;
        }
        
        public async Task ProcessAsync(string fileName, Guid playlistId, string trackName)
        {
            var unprocessedAudioFileContent = new MemoryStream();
            var result = new MemoryStream();

            await _audioStorage.ReadUnprocessedAudioFileAsync(fileName, unprocessedAudioFileContent);
            
            try
            {
                _audioConversionService.ConvertToMp3(unprocessedAudioFileContent, result);
            }
            catch (Exception ex)
            {
                // File can't be converted

                await _audioConversionMessageBroker
                    .SendFileConversionResult(false, null, playlistId, trackName);

                await CleanUpUnprocessedFileAsync(fileName);

                return;
            }

            var newFileName = $"{fileName}.mp3";

            await _audioStorage.UploadAudioFileAsync(newFileName, result);

            try
            {
                throw new Exception("");

                await _audioConversionMessageBroker
                    .SendFileConversionResult(true, newFileName, playlistId, trackName);
            }
            catch
            {
                try
                {
                    await _audioStorage.DeleteAudioFileAsync(newFileName);
                }
                catch
                {
                    // TODO: Log that file wasn't cleaned up
                    throw;
                }

                throw;
            }

            await CleanUpUnprocessedFileAsync(fileName);
        }

        private async Task CleanUpUnprocessedFileAsync(string fileName)
        {
            try
            {
                await _audioStorage.DeleteUnprocessedAudioFileAsync(fileName);
            }
            catch
            {
                // TODO Log that file wasn't cleaned up

                // Don't bubble exception because file was processed
                // we just can't clean up old file
                // so we don't want message be marked as dead letter
            }
        }
    }
}
