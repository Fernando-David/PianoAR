using UnityEngine;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

public class LectorMIDI : MonoBehaviour
{
    [Tooltip("Nombre del archivo MIDI con todo y extension")]
    public string nombreArchivoMIDI = "EnderLilies.mid";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ProcesarArchivoMIDI();
    }

    // Update is called once per frame
    void Update()
    {
        

    }
    void ProcesarArchivoMIDI()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, nombreArchivoMIDI);
        if (File.Exists(filePath))
        {
            Debug.LogError($"No se encontró el archivo MIDI en la ruta: {filePath}");
            return;
        }
        // Leer archivo MIDI
        MidiFile midiFile = MidiFile.Read(filePath);
        //Obtencion del tempo
        TempoMap tempoMap = midiFile.GetTempoMap();
        //Extraer notas agrupadas
        var notes = midiFile.GetNotes().ToList();

        Debug.Log($"Archivo cargado. Total de notas encontradas: {notes.Count}");

        for (int i = 0; i < Mathf.Min(10, notes.Count); i++)
        {
            var note = notes[i];

            MetricTimeSpan startTime = note.TimeAs<MetricTimeSpan>(tempoMap);
            MetricTimeSpan duration = note.LengthAs<MetricTimeSpan>(tempoMap);

            Debug.Log($"Nota {i + 1}: {note.NoteName}{note.Octave} | " +
                $"Inicia en: {startTime.Minutes:D2}:{startTime.Seconds:D2}:{startTime.Milliseconds:D3} | " +
                $"Duracion: {duration.TotalMicroseconds/1000f} ms | "+
                $"Número MIDI (Pitch): {note.NoteNumber}");
        }

    }
}
