package com.crediplus.crediplus_authenticator

import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder

object CrediPlusBackend {

    fun aprobar(
        baseUrl: String,
        solicitudId: String,
        numero: String
    ): Boolean {

        return post(
            "${
                baseUrl.trimEnd('/')
            }/Login/ResponderSolicitudAuthenticator",
            mapOf(
                "solicitudId" to solicitudId,
                "numero" to numero
            )
        )
    }


    fun cancelar(
        baseUrl: String,
        solicitudId: String
    ): Boolean {

        return post(
            "${
                baseUrl.trimEnd('/')
            }/Login/CancelarSolicitudAuthenticator",
            mapOf(
                "solicitudId" to solicitudId
            )
        )
    }


    private fun post(
        direccion: String,
        datos: Map<String, String>
    ): Boolean {

        val contenido =
            datos.entries.joinToString("&") {

                "${
                    URLEncoder.encode(
                        it.key,
                        "UTF-8"
                    )
                }=${
                    URLEncoder.encode(
                        it.value,
                        "UTF-8"
                    )
                }"
            }

        val conexion =
            URL(direccion)
                .openConnection()
                    as HttpURLConnection

        return try {

            conexion.requestMethod =
                "POST"

            conexion.doOutput =
                true

            conexion.connectTimeout =
                10000

            conexion.readTimeout =
                10000

            conexion.setRequestProperty(
                "Content-Type",
                "application/x-www-form-urlencoded"
            )

            conexion.outputStream.use {
                    salida ->

                salida.write(
                    contenido
                        .toByteArray(
                            Charsets.UTF_8
                        )
                )
            }

            val codigo =
                conexion.responseCode

            val texto =
                if (
                    codigo in 200..299
                ) {

                    conexion.inputStream
                        .bufferedReader()
                        .use {
                            it.readText()
                        }

                } else {

                    return false
                }

            JSONObject(
                texto
            ).optBoolean(
                "ok",
                false
            )

        } catch (_: Exception) {

            false

        } finally {

            conexion.disconnect()
        }
    }
}